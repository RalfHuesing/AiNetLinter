#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_symbol_body</c>: liefert den vollstaendigen Body eines einzelnen C#-Symbols
/// (Methode, Konstruktor, Property, Indexer, Event). Akzeptiert sowohl eine stabile
/// DocumentationCommentId (z. B. <c>M:AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync</c>)
/// als auch das klassische Datei:Zeile:Spalte-Format bzw. einen qualifizierten Namen.
/// Hart gekappt bei <paramref name="maxBodyLines"/> Zeilen (Default 80), mit Ellipse-Indikator
/// und Voll-Laengen-Hinweis am Ende. Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class GetSymbolBodyTool
{
    internal const int DefaultMaxBodyLines = 80;

    /// <summary>Textmarker, den <see cref="ExtractSymbolBody"/> nur bei tatsaechlicher
    /// maxBodyLines-Kappung anhaengt — Grundlage fuer die Sufficiency-Hinweis-Entscheidung in
    /// <see cref="ExecuteAsync"/> (siehe <see cref="McpSufficiencyHints"/>).</summary>
    private const string TruncationMarker = "// ... truncated, total ";

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, int maxBodyLines, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: \"M:Namespace.Klasse.Methode\", \"Datei.cs:42:10\" oder \"Klasse.Methode\".");
        }

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct);
            if (error is not null) return error;
            if (symbol is null) return McpToolResults.SymbolNotFound(symbolIdentifier);

            var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
            var idSuffix = TryGetDeclarationId(symbol);
            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var body = ExtractSymbolBody(symbol, maxBodyLines, outputRoot);
            var isTruncated = body.Contains(TruncationMarker, StringComparison.Ordinal);

            var markdown = $"### {symbol.Kind}: {symbol.ToDisplayString()} — `{Path.GetFileName(outputRoot)}/{ToRelative(outputRoot, symbol)}`\n\n" +
                           (idSuffix is null ? "" : $"id: `{idSuffix}`\n\n") +
                           "```csharp\n" +
                           body +
                           "\n```";

            // Sufficiency-Hinweis nur fuer den vollstaendigen Body — ein per maxBodyLines
            // gekappter Body traegt bereits seinen eigenen "truncated, maxBodyLines erhoehen"-
            // Hinweis (siehe ExtractSymbolBody), der widerspruechlich waere neben "vollstaendig".
            var final = isTruncated ? markdown : McpSufficiencyHints.Append(markdown);
            return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, final));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_symbol_body: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    private static string? TryGetDeclarationId(ISymbol symbol)
    {
        try
        {
            return DocumentationCommentId.CreateDeclarationId(symbol);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

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
