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
/// <c>symbolIdentifiers</c>-Array; ein einzelnes Symbol ist ein Array-Eintrag.
/// </summary>
internal static class GetSymbolBodyTool
{
    internal const int DefaultMaxBodyLines = 80;

    /// <summary>Textmarker, den <see cref="ExtractSymbolBody"/> nur bei tatsaechlicher
    /// maxBodyLines-Kappung anhaengt — Grundlage fuer die Sufficiency-Hinweis-Entscheidung in
    /// <see cref="ExecuteAsync"/> (siehe <see cref="McpSufficiencyHints"/>).</summary>
    private const string TruncationMarker = "// ... truncated, total ";

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = McpBatchArguments.Normalize(symbolIdentifiers, StringComparer.Ordinal);
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
        AssemblyAnalysisLease lease,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var solution = lease.Server.GetCurrentSolution();
        if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());
        var identifiers = McpBatchArguments.Normalize(symbolIdentifiers, StringComparer.Ordinal);
        if (identifiers.Count == 0)
        {
            return Task.FromResult(McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint));
        }

        return RenderSymbolBodiesAsync(
            solution, identifiers, maxBodyLines, lease.Server.AssemblySymbolIdentity, lease, ct);
    }

    private static async Task<CallToolResult> RenderSymbolBodiesAsync(
        Solution solution,
        IReadOnlyList<string> identifiers,
        int maxBodyLines,
        AnalysisSymbolIdentity? assemblyIdentity,
        AssemblyAnalysisLease? lease,
        CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
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
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, final));
    }

    private static async Task<CallToolResult?> RenderSingleSymbolAsync(
        RenderSingleSymbolRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var identifier = request.Identifier;
        var totalCount = request.TotalCount;
        var maxBodyLines = request.MaxBodyLines;
        var outputRoot = request.OutputRoot;
        var mb = request.Markdown;
        var assemblyIdentity = request.AssemblyIdentity;
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct, assemblyIdentity);

        if (error is not null)
        {
            if (totalCount == 1) return error;
            mb.Heading(3, $"Symbol `{identifier}` nicht aufgeloest");
            mb.BlankLine();
            var errorText = error.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Fehler beim Aufloesen.";
            mb.Line(errorText.Trim());
            return null;
        }

        if (symbol is null)
        {
            if (totalCount == 1) return McpToolResults.SymbolNotFound(identifier);
            mb.Heading(3, $"Symbol nicht gefunden: `{identifier}`");
            return null;
        }

        var idSuffix = assemblyIdentity?.Format(symbol.TryGetDocCommentId() ?? CallGraphTraversal.GetStableSymbolId(symbol))
            ?? symbol.TryGetDocCommentId();
        var bodyResolution = request.Lease is not null && request.Lease.Context.Origin.IsDecompiled
            ? await request.Lease.ResolveBodyAsync(symbol, maxBodyLines, ct).ConfigureAwait(false)
            : CreateSourceBodyResolution(symbol, maxBodyLines, outputRoot);

        mb.Heading(3, $"{symbol.Kind}: {symbol.ToDisplayString()} — `{Path.GetFileName(outputRoot)}/{ToRelative(outputRoot, symbol)}`");
        mb.BlankLine();
        if (!string.Equals(identifier, idSuffix, StringComparison.Ordinal))
        {
            mb.Line($"angefordert: `{identifier}`");
        }
        if (idSuffix is not null)
        {
            mb.Line($"id: `{idSuffix}`");
        }
        mb.Line($"bodyAvailability: `{bodyResolution.BodyAvailability}`; contentMode: `{bodyResolution.ContentMode}`");
        if (!string.IsNullOrWhiteSpace(bodyResolution.Hint)) mb.Line($"Hinweis: {bodyResolution.Hint}");
        mb.BlankLine();
        mb.CodeBlock("csharp", bodyResolution.Body ?? ExtractSymbolBody(symbol, maxBodyLines, outputRoot));
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
        AssemblyAnalysisLease? Lease);

    private static string ToRelative(string outputRoot, ISymbol symbol)
    {
        var path = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(path)) return symbol.ToDisplayString();
        return PathNormalizer.ToRelative(outputRoot, path);
    }

    private static string ExtractSymbolBody(ISymbol symbol, int maxBodyLines, string outputRoot)
    {
        var normalized = maxBodyLines < 1 ? 1 : maxBodyLines;
        var declaringReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaringReference is null)
        {
            return $"// Kein Quell-Syntax verfuegbar fuer '{symbol.ToDisplayString()}' (externes Symbol).";
        }

        var syntax = declaringReference.GetSyntax();
        var text = syntax.ToFullString();
        var lines = text.Split('\n');
        if (lines.Length <= normalized)
        {
            return text.TrimEnd();
        }

        var truncated = string.Join("\n", lines.Take(normalized));
        return truncated.TrimEnd()
            + $"\n// ... truncated, total {lines.Length} Zeilen, maxBodyLines erhoehen fuer mehr";
    }

    private static AssemblyBodyResolution CreateSourceBodyResolution(
        ISymbol symbol,
        int maxBodyLines,
        string outputRoot)
    {
        var hasSyntax = symbol.DeclaringSyntaxReferences.Any();
        var unavailable = symbol.ContainingType?.TypeKind == TypeKind.Interface
            || !hasSyntax
            || symbol switch
            {
                IMethodSymbol method => method.IsAbstract || AssemblyBodySyntax.HasExternModifier(method),
                IPropertySymbol property => property.GetMethod?.IsAbstract == true
                    || property.SetMethod?.IsAbstract == true
                    || AssemblyBodySyntax.HasExternModifier(property.GetMethod)
                    || AssemblyBodySyntax.HasExternModifier(property.SetMethod),
                IEventSymbol eventSymbol => eventSymbol.AddMethod?.IsAbstract == true
                    || eventSymbol.RemoveMethod?.IsAbstract == true,
                _ => false,
            };
        var hint = symbol.ContainingType?.TypeKind == TypeKind.Interface
            ? "Interfaces stellen keinen ausführbaren Body bereit."
            : !hasSyntax
                ? "Für das Symbol ist kein Quell-Syntax verfügbar."
                : unavailable ? "Das Symbol ist abstract oder extern und besitzt keinen Body." : null;
        return new(
            ExtractSymbolBody(symbol, maxBodyLines, outputRoot),
            unavailable ? "unavailable" : "available",
            "source",
            hint);
    }

}
