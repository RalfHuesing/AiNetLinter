#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_symbol_body</c>: liefert den vollstaendigen Body eines oder mehrerer C#-Symbole
/// (Methode, Konstruktor, Property, Indexer, Event). Erwartet ausschliesslich das
/// <c>symbolIdentifiers</c>-Array.
/// </summary>
internal static partial class GetSymbolBodyTool
{
    internal const int DefaultMaxBodyLines = 80;
    internal const int DefaultMaxResponseBytes = 32 * 1024;

    /// <summary>Textmarker, den <see cref="ExtractSymbolBody"/> nur bei tatsaechlicher
    /// maxBodyLines-Kappung anhaengt — Grundlage fuer die Sufficiency-Hinweis-Entscheidung in
    /// <see cref="ExecuteAsync"/>).</summary>
    private const string TruncationMarker = "// ... truncated, total ";

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        GetSymbolBodyRequest request,
        CancellationToken ct)
    {
        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes))
        {
            return InvalidResponseBudget();
        }
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = McpBatchArguments.Normalize(request.SymbolIdentifiers, StringComparer.Ordinal);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint);
        }

        try
        {
            return await RenderSymbolBodiesAsync(
                new RenderSymbolBodiesRequest(solution, identifiers, request.EffectiveMaxBodyLines, request.StartLine,
                    request.MaxResponseBytes, state.HandoffSymbolIdentity, null), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_symbol_body: {ex.Message}",
                context: string.Join(", ", identifiers));
        }
    }

    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct) =>
        ExecuteAsync(state, new GetSymbolBodyRequest(symbolIdentifiers, MaxBodyLines: maxBodyLines), ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        GetSymbolBodyRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var identifiers = McpBatchArguments.Normalize(request.SymbolIdentifiers, StringComparer.Ordinal);
        if (!identifiers.Any(identifier =>
                AssemblySearchPlan.Create(identifier, includeReferences: false).Mode == AssemblySearchMode.SymbolOwnerOnly))
        {
            return await ExecuteAsync((IAssemblyBodyContext)lease, request, ct).ConfigureAwait(false);
        }

        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes)) return InvalidResponseBudget();
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint);
        }

        var requests = new List<RenderSingleSymbolRequest>(identifiers.Count);
        var navigations = new List<AssemblyNavigationSummary>();
        foreach (var identifier in identifiers)
        {
            var plan = AssemblySearchPlan.Create(identifier, includeReferences: false);
            var targetLease = lease;
            if (plan.Mode == AssemblySearchMode.SymbolOwnerOnly)
            {
                var resolved = await AssemblySymbolResolver.ResolveAsync(lease, identifier, plan, ct).ConfigureAwait(false);
                if (resolved.Error is not null) return resolved.Error;
                targetLease = resolved.Target!.Lease;
                navigations.Add(resolved.Navigation);
            }

            var solution = targetLease.Server.GetCurrentSolution();
            if (solution is null) return McpToolResults.SolutionNotLoaded();
            requests.Add(new(
                solution,
                identifier,
                identifiers.Count,
                request.EffectiveMaxBodyLines,
                request.StartLine,
                Path.GetDirectoryName(solution.FilePath) ?? string.Empty,
                targetLease.Server.AssemblySymbolIdentity,
                targetLease.Context.Origin));
        }

        var result = await RenderSymbolBodiesAsync(requests, request.MaxResponseBytes, ct).ConfigureAwait(false);
        if (navigations.Count > 0)
        {
            result = AddAssemblyNavigation(
                result,
                navigations.Aggregate(AssemblyNavigationSupport.MergeSummaries));
        }

        return AssemblyPublicContract.Project(result);
    }

    internal static async Task<CallToolResult> ExecuteAsync(
        IAssemblyBodyContext lease,
        GetSymbolBodyRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes))
        {
            return InvalidResponseBudget();
        }
        var solution = lease.Solution;
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        var identifiers = McpBatchArguments.Normalize(request.SymbolIdentifiers, StringComparer.Ordinal);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint);
        }

        var result = await RenderSymbolBodiesAsync(
            new RenderSymbolBodiesRequest(solution, identifiers, request.EffectiveMaxBodyLines, request.StartLine,
                request.MaxResponseBytes, lease.AssemblySymbolIdentity, lease.Origin), ct)
            .ConfigureAwait(false);
        return AssemblyPublicContract.Project(result);
    }

    internal static Task<CallToolResult> ExecuteAsync(
        IAssemblyBodyContext lease,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct) =>
        ExecuteAsync(lease, new GetSymbolBodyRequest(symbolIdentifiers, MaxBodyLines: maxBodyLines), ct);

    private static CallToolResult AddAssemblyNavigation(
        CallToolResult result,
        AssemblyNavigationSummary navigation)
    {
        if (result.StructuredContent is not { } structured
            || JsonNode.Parse(structured.GetRawText()) is not JsonObject payload)
        {
            return result;
        }

        payload["navigation"] = JsonSerializer.SerializeToNode(navigation, McpJsonOptions.Default);
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };
    }


    private static async Task<CallToolResult> RenderSymbolBodiesAsync(
        RenderSymbolBodiesRequest request,
        CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? string.Empty;
        var requests = request.Identifiers.Select(identifier => new RenderSingleSymbolRequest(
            request.Solution,
            identifier,
            request.Identifiers.Count,
            request.MaxBodyLines,
            request.StartLine,
            outputRoot,
            request.AssemblyIdentity,
            request.AssemblyOrigin));
        return await RenderSymbolBodiesAsync(requests, request.MaxResponseBytes, ct).ConfigureAwait(false);
    }

    private static async Task<CallToolResult> RenderSymbolBodiesAsync(
        IEnumerable<RenderSingleSymbolRequest> requests,
        int maxResponseBytes,
        CancellationToken ct)
    {
        var requestList = requests.ToList();
        var units = new List<SymbolBodyRenderUnit>(requestList.Count);
        foreach (var request in requestList)
        {
            var rendered = await RenderSingleSymbolAsync(request, ct).ConfigureAwait(false);
            if (rendered.EarlyError is not null) return rendered.EarlyError;
            units.Add(rendered.Unit!);
        }

        return CreateBudgetedResult(units, requestList.Count, maxResponseBytes);
    }

    private static async Task<RenderSingleSymbolResult> RenderSingleSymbolAsync(
        RenderSingleSymbolRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, request.Identifier, ct, request.AssemblyIdentity);

        if (symbol is null || error is not null || symbol is ILocalSymbol or IParameterSymbol)
        {
            var enclosing = await TryResolveEnclosingMemberForBodyAsync(solution, request.Identifier, ct).ConfigureAwait(false);
            if (enclosing is not null)
            {
                symbol = enclosing;
                error = null;
            }
        }

        if (error is not null) return RenderResolutionError(request, error);

        if (symbol is null) return RenderMissingSymbol(request);

        return RenderResolvedSymbol(request, symbol);
    }

    private static async Task<ISymbol?> TryResolveEnclosingMemberForBodyAsync(
        Solution solution,
        string identifier,
        CancellationToken ct)
    {
        if (!SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out _)
            && !SymbolIdentifierResolver.TryParseLineOnlyPosition(identifier, out path, out line))
        {
            return null;
        }

        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, path);
        if (document is null) return null;

        var text = await document.GetTextAsync(ct).ConfigureAwait(false);
        if (text is null || line < 1 || line > text.Lines.Count) return null;

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var sm = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root is null || sm is null) return null;

        var lineSpan = text.Lines[line - 1].Span;
        return SymbolIdentifierResolver.TryFindEnclosingMember(root, lineSpan, sm);
    }

    private static RenderSingleSymbolResult RenderResolutionError(
        RenderSingleSymbolRequest request,
        CallToolResult error)
    {
        if (request.TotalCount == 1) return new(error, null);
        var markdown = new MarkdownBuilder();
        markdown.Heading(3, $"Symbol `{request.Identifier}` nicht aufgeloest");
        markdown.BlankLine();
        var errorText = error.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Fehler beim Aufloesen.";
        markdown.Line(errorText.Trim());
        return new(null, new SymbolBodyRenderUnit(null, markdown.Build().TrimEnd()));
    }

    private static RenderSingleSymbolResult RenderMissingSymbol(RenderSingleSymbolRequest request)
    {
        if (request.TotalCount == 1) return new(McpToolResults.SymbolNotFound(request.Identifier), null);
        var markdown = new MarkdownBuilder();
        markdown.Heading(3, $"Symbol nicht gefunden: `{request.Identifier}`");
        return new(null, new SymbolBodyRenderUnit(null, markdown.Build().TrimEnd()));
    }

    private static RenderSingleSymbolResult RenderResolvedSymbol(
        RenderSingleSymbolRequest request,
        ISymbol symbol)
    {
        var idSuffix = request.AssemblyIdentity?.FormatHandoff(symbol)
            ?? symbol.TryGetDocCommentId();
        var bodyResolution = SourceSymbolBodyResolver.Resolve(symbol, request.MaxBodyLines, request.AssemblyOrigin, request.StartLine);

        var markdown = new MarkdownBuilder();
        markdown.Heading(3, $"{symbol.Kind}: {symbol.ToDisplayString()} — `{FormatLocation(request, symbol)}`");
        markdown.BlankLine();
        if (!string.Equals(request.Identifier, idSuffix, StringComparison.Ordinal)
            && !SymbolHandoffIdentifier.TryParse(request.Identifier, out _))
        {
            markdown.Line($"angefordert: `{request.Identifier}`");
        }
        markdown.Line($"bodyAvailability: `{bodyResolution.BodyAvailability}`; contentMode: `{bodyResolution.ContentMode}`");
        if (bodyResolution.TotalBodyLines > 0)
        {
            markdown.Line($"Zeilen: {bodyResolution.DisplayedStartLine}-{bodyResolution.DisplayedEndLine} von {bodyResolution.TotalBodyLines}");
        }
        if (!string.IsNullOrWhiteSpace(bodyResolution.Hint)) markdown.Line($"Hinweis: {bodyResolution.Hint}");
        markdown.BlankLine();
        markdown.CodeBlock("csharp", bodyResolution.Body ?? "// Für dieses Symbol ist kein dekompilierbarer Body verfügbar.");
        var location = symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        var lineSpan = location?.GetLineSpan();
        var entry = new SymbolBodyEntry(
            request.Identifier,
            idSuffix,
            idSuffix is null ? null : "member",
            PathNormalizer.ToRelative(request.OutputRoot, location?.SourceTree?.FilePath ?? ""),
            lineSpan is null ? 0 : lineSpan.Value.StartLinePosition.Line + 1,
            bodyResolution.Body,
            bodyResolution.BodyAvailability,
            bodyResolution.ContentMode,
            bodyResolution.Body?.Contains(TruncationMarker, StringComparison.Ordinal) == true,
            bodyResolution.TotalBodyLines,
            bodyResolution.DisplayedStartLine,
            bodyResolution.DisplayedEndLine,
            bodyResolution.HasMoreLines);
        return new(null, new SymbolBodyRenderUnit(entry, markdown.Build().TrimEnd()));
    }

    private sealed record RenderSingleSymbolRequest(
        Solution Solution,
        string Identifier,
        int TotalCount,
        int MaxBodyLines,
        int StartLine,
        string OutputRoot,
        AnalysisSymbolIdentity? AssemblyIdentity,
        AssemblyOrigin? AssemblyOrigin);

    private static CallToolResult CreateBudgetedResult(
        IReadOnlyList<SymbolBodyRenderUnit> units,
        int requestedCount,
        int maxResponseBytes)
    {
        var kept = units.ToList();
        var truncated = false;
        while (CombinedBytes(kept, requestedCount, truncated) > maxResponseBytes && kept.Count > 0)
        {
            kept.RemoveAt(kept.Count - 1);
            truncated = true;
        }

        if (CombinedBytes(kept, requestedCount, truncated) > maxResponseBytes)
        {
            return BudgetTooSmall(maxResponseBytes);
        }

        return CreateResult(kept, requestedCount, truncated);
    }

    private static CallToolResult CreateResult(
        IReadOnlyList<SymbolBodyRenderUnit> units,
        int requestedCount,
        bool truncated)
    {
        var markdown = string.Join("\n\n---\n\n", units.Select(unit => unit.Markdown));
        if (truncated)
        {
            markdown += (markdown.Length == 0 ? string.Empty : "\n\n")
                + "[Antwort wegen maxResponseBytes begrenzt — vollständige Symbol-Body-Einheiten ausgelassen; maxResponseBytes erhöhen oder symbolIdentifiers verfeinern]";
        }

        return McpToolResults.Text(
            markdown,
            new SymbolBodyBatchDto(units.Where(unit => unit.Entry is not null).Select(unit => unit.Entry!).ToList(), requestedCount));
    }

    private static int CombinedBytes(
        IReadOnlyList<SymbolBodyRenderUnit> units,
        int requestedCount,
        bool truncated)
    {
        var result = CreateResult(units, requestedCount, truncated);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        return Encoding.UTF8.GetByteCount(text)
            + JsonSerializer.SerializeToUtf8Bytes(result.StructuredContent!.Value, McpJsonOptions.Default).Length;
    }

    private static CallToolResult BudgetTooSmall(int budget) =>
        McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={budget} ist zu klein für die vollständige minimale Symbol-Body-Projektion.",
            new McpErrorParameters(
                Hint: "maxResponseBytes erhöhen; Symbol-Bodies werden nur als vollständige Einheiten gekürzt.",
                FieldPath: "$.maxResponseBytes"));

    private static CallToolResult InvalidResponseBudget() =>
        McpToolResults.InvalidArgument(
            $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
            $"maxResponseBytes weglassen oder einen Wert zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} setzen.",
            "$.maxResponseBytes");

    private static string ToRelative(string outputRoot, ISymbol symbol)
    {
        var path = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(path)) return symbol.ToDisplayString();
        return PathNormalizer.ToRelative(outputRoot, path);
    }

    private static string FormatLocation(RenderSingleSymbolRequest request, ISymbol symbol)
    {
        var path = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(path)) return symbol.ToDisplayString();
        return request.AssemblyIdentity is null
            ? $"{Path.GetFileName(request.OutputRoot)}/{ToRelative(request.OutputRoot, symbol)}"
            : Path.GetFullPath(path);
    }

}

internal sealed record SymbolBodyRenderUnit(SymbolBodyEntry? Entry, string Markdown);

internal sealed record RenderSymbolBodiesRequest(
    Solution Solution,
    IReadOnlyList<string> Identifiers,
    int MaxBodyLines,
    int StartLine,
    int MaxResponseBytes,
    AnalysisSymbolIdentity? AssemblyIdentity,
    AssemblyOrigin? AssemblyOrigin);

internal sealed record RenderSingleSymbolResult(CallToolResult? EarlyError, SymbolBodyRenderUnit? Unit);

internal sealed record SymbolBodyBatchDto(
    IReadOnlyList<SymbolBodyEntry> Results,
    int RequestedCount);

internal sealed record SymbolBodyEntry(
    string RequestedIdentifier,
    string? Id,
    string? HandoffKind,
    string FilePath,
    int StartLine,
    string? Body,
    string BodyAvailability,
    string ContentMode,
    bool IsTruncated,
    int TotalBodyLines = 0,
    int DisplayedStartLine = 1,
    int DisplayedEndLine = 0,
    bool HasMoreLines = false);

internal sealed record GetSymbolBodyRequest(
    string[]? SymbolIdentifiers = null,
    int MaxBodyLines = GetSymbolBodyTool.DefaultMaxBodyLines,
    int StartLine = 1,
    int? EndLine = null,
    int MaxResponseBytes = GetSymbolBodyTool.DefaultMaxResponseBytes)
{
    internal int EffectiveMaxBodyLines =>
        EndLine.HasValue
            ? Math.Max(1, EndLine.Value - Math.Max(1, StartLine) + 1)
            : MaxBodyLines;
}
