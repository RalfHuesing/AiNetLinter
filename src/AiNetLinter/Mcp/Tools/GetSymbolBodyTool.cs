#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_symbol_body</c>: liefert den vollstaendigen Body eines oder mehrerer C#-Symbole
/// (Methode, Konstruktor, Property, Indexer, Event). Erwartet ausschliesslich das
/// <c>symbolIdentifiers</c>-Array; <c>symbolIdentifier</c> bleibt als Alias fuer genau ein
/// Symbol verfuegbar.
/// </summary>
internal static class GetSymbolBodyTool
{
    internal const int DefaultMaxBodyLines = 80;

    /// <summary>Textmarker, den <see cref="ExtractSymbolBody"/> nur bei tatsaechlicher
    /// maxBodyLines-Kappung anhaengt — Grundlage fuer die Sufficiency-Hinweis-Entscheidung in
    /// <see cref="ExecuteAsync"/> (siehe <see cref="McpSufficiencyHints"/>).</summary>
    private const string TruncationMarker = "// ... truncated, total ";

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct,
        string? symbolIdentifier = null)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = NormalizeIdentifiers(symbolIdentifiers, symbolIdentifier);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint);
        }

        try
        {
            return await RenderSymbolBodiesAsync(solution, identifiers, maxBodyLines, state.AssemblySymbolIdentity, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_symbol_body: {ex.Message}",
                context: string.Join(", ", identifiers));
        }
    }

    internal static Task<CallToolResult> ExecuteAsync(
        IAssemblyBodyContext lease,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct,
        string? symbolIdentifier = null)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var solution = lease.Solution;
        if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());
        var identifiers = NormalizeIdentifiers(symbolIdentifiers, symbolIdentifier);
        if (identifiers.Count == 0)
        {
            return Task.FromResult(McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint));
        }

        return RenderSymbolBodiesAsync(
            solution, identifiers, maxBodyLines, lease.AssemblySymbolIdentity, lease, ct);
    }

    private static IReadOnlyList<string> NormalizeIdentifiers(
        string[]? symbolIdentifiers,
        string? symbolIdentifier)
    {
        var identifiers = McpBatchArguments.Normalize(symbolIdentifiers, StringComparer.Ordinal);
        return identifiers.Count > 0 || string.IsNullOrWhiteSpace(symbolIdentifier)
            ? identifiers
            : McpBatchArguments.Normalize([symbolIdentifier], StringComparer.Ordinal);
    }

    private static async Task<CallToolResult> RenderSymbolBodiesAsync(
        Solution solution,
        IReadOnlyList<string> identifiers,
        int maxBodyLines,
        AnalysisSymbolIdentity? assemblyIdentity,
        IAssemblyBodyContext? lease,
        CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var mb = new MarkdownBuilder();

        for (var i = 0; i < identifiers.Count; i++)
        {
            if (i > 0) mb.Divider();

            var earlyError = await RenderSingleSymbolAsync(
                new RenderSingleSymbolRequest(
                    solution, identifiers[i], identifiers.Count, maxBodyLines, outputRoot, mb, assemblyIdentity, lease),
                ct);

            if (earlyError != null) return earlyError;
        }

        var markdown = mb.Build().TrimEnd();
        var isTruncated = markdown.Contains(TruncationMarker, StringComparison.Ordinal);
        var final = isTruncated ? markdown : McpSufficiencyHints.Append(markdown);
        return McpToolResults.Text(final);
    }

    private static async Task<CallToolResult?> RenderSingleSymbolAsync(
        RenderSingleSymbolRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, request.Identifier, ct, request.AssemblyIdentity);

        if (error is not null) return RenderResolutionError(request, error);

        if (symbol is null) return RenderMissingSymbol(request);

        return RenderResolvedSymbol(request, symbol);
    }

    private static CallToolResult? RenderResolutionError(
        RenderSingleSymbolRequest request,
        CallToolResult error)
    {
        if (request.TotalCount == 1) return error;
        request.Markdown.Heading(3, $"Symbol `{request.Identifier}` nicht aufgeloest");
        request.Markdown.BlankLine();
        var errorText = error.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Fehler beim Aufloesen.";
        request.Markdown.Line(errorText.Trim());
        return null;
    }

    private static CallToolResult? RenderMissingSymbol(RenderSingleSymbolRequest request)
    {
        if (request.TotalCount == 1) return McpToolResults.SymbolNotFound(request.Identifier);
        request.Markdown.Heading(3, $"Symbol nicht gefunden: `{request.Identifier}`");
        return null;
    }

    private static CallToolResult? RenderResolvedSymbol(
        RenderSingleSymbolRequest request,
        ISymbol symbol)
    {
        var idSuffix = request.AssemblyIdentity?.Format(symbol.TryGetDocCommentId() ?? CallGraphTraversal.GetStableSymbolId(symbol))
            ?? symbol.TryGetDocCommentId();
        var bodyResolution = SourceSymbolBodyResolver.Resolve(symbol, request.MaxBodyLines);

        request.Markdown.Heading(3, $"{symbol.Kind}: {symbol.ToDisplayString()} — `{Path.GetFileName(request.OutputRoot)}/{ToRelative(request.OutputRoot, symbol)}`");
        request.Markdown.BlankLine();
        if (!string.Equals(request.Identifier, idSuffix, StringComparison.Ordinal))
        {
            request.Markdown.Line($"angefordert: `{request.Identifier}`");
        }
        if (idSuffix is not null)
        {
            request.Markdown.Line($"id: `{idSuffix}`");
        }
        request.Markdown.Line($"bodyAvailability: `{bodyResolution.BodyAvailability}`; contentMode: `{bodyResolution.ContentMode}`");
        if (!string.IsNullOrWhiteSpace(bodyResolution.Hint)) request.Markdown.Line($"Hinweis: {bodyResolution.Hint}");
        request.Markdown.BlankLine();
        request.Markdown.CodeBlock("csharp", bodyResolution.Body ?? "// Für dieses Symbol ist kein dekompilierbarer Body verfügbar.");
        return null;
    }

    private sealed record RenderSingleSymbolRequest(
        Solution Solution,
        string Identifier,
        int TotalCount,
        int MaxBodyLines,
        string OutputRoot,
        MarkdownBuilder Markdown,
        AnalysisSymbolIdentity? AssemblyIdentity,
        IAssemblyBodyContext? Lease);

    private static string ToRelative(string outputRoot, ISymbol symbol)
    {
        var path = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(path)) return symbol.ToDisplayString();
        return PathNormalizer.ToRelative(outputRoot, path);
    }

}
