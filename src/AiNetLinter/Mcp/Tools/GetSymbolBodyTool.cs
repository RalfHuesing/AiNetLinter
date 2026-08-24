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
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_symbol_body</c>: liefert den vollstaendigen Body eines oder mehrerer C#-Symbole
/// (Methode, Konstruktor, Property, Indexer, Event). Akzeptiert sowohl einzelne Identifiers als auch
/// Batch-Arrays fuer effizientes Laden in einem einzigen Turn.
/// </summary>
internal static class GetSymbolBodyTool
{
    internal const int DefaultMaxBodyLines = 80;

    /// <summary>Textmarker, den <see cref="ExtractSymbolBody"/> nur bei tatsaechlicher
    /// maxBodyLines-Kappung anhaengt — Grundlage fuer die Sufficiency-Hinweis-Entscheidung in
    /// <see cref="ExecuteAsync"/> (siehe <see cref="McpSufficiencyHints"/>).</summary>
    private const string TruncationMarker = "// ... truncated, total ";

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, int maxBodyLines, CancellationToken ct) =>
        ExecuteAsync(state, symbolIdentifier, null, maxBodyLines, ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? symbolIdentifier,
        string[]? symbolIdentifiers,
        int maxBodyLines,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = ExtractIdentifiers(symbolIdentifier, symbolIdentifiers);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' oder 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierBatchHint);
        }

        try
        {
            return await RenderSymbolBodiesAsync(solution, identifiers, maxBodyLines, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_symbol_body: {ex.Message}",
                context: string.Join(", ", identifiers));
        }
    }

    private static async Task<CallToolResult> RenderSymbolBodiesAsync(
        Solution solution,
        IReadOnlyList<string> identifiers,
        int maxBodyLines,
        CancellationToken ct)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var mb = new MarkdownBuilder();

        for (var i = 0; i < identifiers.Count; i++)
        {
            if (i > 0) mb.Divider();

            var earlyError = await RenderSingleSymbolAsync(
                solution, identifiers[i], identifiers.Count, maxBodyLines, outputRoot, mb, ct);

            if (earlyError != null) return earlyError;
        }

        var markdown = mb.Build().TrimEnd();
        var isTruncated = markdown.Contains(TruncationMarker, StringComparison.Ordinal);
        var final = isTruncated ? markdown : McpSufficiencyHints.Append(markdown);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, final));
    }

    private static async Task<CallToolResult?> RenderSingleSymbolAsync(
        Solution solution,
        string identifier,
        int totalCount,
        int maxBodyLines,
        string outputRoot,
        MarkdownBuilder mb,
        CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct);

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

        var idSuffix = symbol.TryGetDocCommentId();
        var body = ExtractSymbolBody(symbol, maxBodyLines, outputRoot);

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
        mb.BlankLine();
        mb.CodeBlock("csharp", body);
        return null;
    }

    private static List<string> ExtractIdentifiers(string? symbolIdentifier, string[]? symbolIdentifiers) =>
        McpBatchArguments.Collect(symbolIdentifier, symbolIdentifiers, StringComparer.Ordinal);

    private static string ToRelative(string outputRoot, ISymbol symbol)
    {
        var path = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(path)) return symbol.ToDisplayString();
        return Path.GetRelativePath(outputRoot, path).Replace('\\', '/');
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
}
