#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>find_references</c>: loest einen Symbol-Identifikator (Datei:Zeile:Spalte oder
/// qualifizierter/teil-qualifizierter Name) zu genau einem Roslyn-<see cref="ISymbol"/> auf und
/// liefert dessen Aufrufstellen ueber <see cref="DiffImpactAnalyzer.FindCallSitesAsync"/>. Deckt
/// nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class FindReferencesTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, loest den Identifikator zu einem
    /// Symbol auf und liefert dessen Aufrufstellen als Text. Stellt dem Aufrufstellen-Output
    /// einen
    /// Dateien hat (Roslyn toleriert sie, aber der Agent weiss sonst nicht, dass die Antwort
    /// unvollstaendig sein kann). Defensiver try/catch-Wrapper faengt unerwartete Roslyn-Exceptions
    /// ab und liefert einen strukturierten [ERROR]-Antwort statt eines Server-Crashs 
    /// Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string symbolIdentifier, int maxResults, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        try
        {
            var (symbol, error) = await ResolveSymbolAsync(solution, symbolIdentifier, ct);
            if (error is not null) return error;

            var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);

            if (callSites.Count == 0)
            {
                return McpToolResults.Text(FindSymbolTool.PrependWarning(
                    warning, $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"));
            }

            var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
            return McpToolResults.Text(FindSymbolTool.PrependWarning(
                warning,
                McpTruncation.TruncateLines(callSites, callSites.Count, normalizedMaxResults)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    /// <summary>
    /// Loest <paramref name="identifier"/> entweder ueber eine Datei:Zeile:Spalte-Angabe oder ueber
    /// einen qualifizierten/teil-qualifizierten Namen zu genau einem Symbol auf. Reine Funktion
    /// (Solution rein, Symbol/Fehler raus) ohne Abhaengigkeit von <see cref="McpCodeGraphServer"/> —
    /// direkt unit-testbar.
    /// </summary>
    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        if (SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out var column))
        {
            return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
        }

        return await ResolveByNameAsync(solution, identifier, ct);
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByPositionAsync(
        Solution solution, string identifier, string path, int line, int column, CancellationToken ct)
    {
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, path);
        if (document is null) return (null, McpToolResults.SymbolNotFound(identifier));

        var root = await document.GetSyntaxRootAsync(ct);
        var text = await document.GetTextAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (root is null || text is null || semanticModel is null || line < 1 || line > text.Lines.Count)
        {
            return (null, McpToolResults.SymbolNotFound(identifier));
        }

        var position = text.Lines[line - 1].Start + (column - 1);
        var token = root.FindToken(position);
        var symbol = SymbolIdentifierResolver.ResolveSymbolAtToken(token, semanticModel);

        return symbol is null ? (null, McpToolResults.SymbolNotFound(identifier)) : (symbol, null);
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByNameAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        var lastSegment = SymbolIdentifierResolver.StripParameterList(identifier).Split('.')[^1];
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution, name => name == lastSegment, SymbolFilter.TypeAndMember, ct);

        var candidates = symbols
            .Where(s => SymbolIdentifierResolver.StripParameterList(s.ToDisplayString())
                .EndsWith(identifier, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0) return (null, McpToolResults.SymbolNotFound(identifier));
        if (candidates.Count == 1) return (candidates[0], null);

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = candidates.SelectMany(s => FindSymbolTool.FormatSymbolLocations(s, outputRoot));
        return (null, McpToolResults.AmbiguousSymbol(identifier, lines));
    }
}
