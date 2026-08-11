#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>find_references</c>: loest einen Symbol-Identifikator (stabile
/// DocumentationCommentId, Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter Name) zu
/// genau einem Roslyn-<see cref="ISymbol"/> auf und liefert dessen Aufrufstellen ueber
/// <see cref="DiffImpactAnalyzer.FindCallSitesAsync"/>. Deckt nur .cs-Dateien ab
/// (Roslyn-Symbolgraph). Optionaler <c>depth</c>-Parameter (Default 1, hard cap 3) loest
/// transitive Aufrufstellen ueber <see cref="CallGraphTraversal"/> auf und aggregiert sie zu
/// einer Top-N-Antwort.
/// </summary>
internal static class FindReferencesTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, loest den Identifikator zu einem
    /// Symbol auf und liefert dessen Aufrufstellen als Text. Stellt dem Aufrufstellen-Output
    /// einen Warnhinweis voran, falls die Solution Compile-Fehler in einzelnen Dateien hat
    /// (Roslyn toleriert sie, aber der Agent weiss sonst nicht, dass die Antwort unvollstaendig
    /// sein kann). Defensiver try/catch-Wrapper faengt unerwartete Roslyn-Exceptions ab und
    /// liefert einen strukturierten [ERROR]-Antwort statt eines Server-Crashs (Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, int maxResults, int depth, CancellationToken ct)
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
            var (symbol, error) = await ResolveSymbolAsync(solution, symbolIdentifier, ct);
            if (error is not null) return error;

            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
            var clampedDepth = Math.Clamp(depth, 1, CallGraphTraversal.MaxRecursionDepth);
            string body;
            bool isTruncated;
            // StructuredContent nur fuer den depth=1-Flachfall — CallGraphTraversal
            // (depth>1) baut Locations intern als reine Strings ohne strukturiertes Zwischenmodell;
            // eine strukturierte Erweiterung dort waere ein groesserer Umbau (bewusst ausgelassen).
            IReadOnlyList<CallSiteEntry>? entries = null;

            if (clampedDepth == 1)
            {
                var callSiteEntries = await DiffImpactAnalyzer.FindCallSiteEntriesAsync(symbol!, solution);
                if (callSiteEntries.Count == 0)
                {
                    // Zero Treffer ist ein vollstaendiges, definitives Ergebnis (kein Aufrufer
                    // existiert) — Sufficiency-Hinweis gilt.
                    return McpToolResults.Text(McpSufficiencyHints.Append(FindSymbolTool.PrependWarning(
                        warning, $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'")));
                }
                isTruncated = callSiteEntries.Count > normalizedMaxResults;
                var callSites = callSiteEntries.Select(DiffImpactAnalyzer.FormatCallSite).ToList();
                body = McpTruncation.TruncateLines(callSites, callSites.Count, normalizedMaxResults);
                entries = callSiteEntries.Count <= normalizedMaxResults
                    ? callSiteEntries
                    : callSiteEntries.Take(normalizedMaxResults).ToList();
            }
            else
            {
                body = await CallGraphTraversal.ExpandAndFormatAsync(
                    solution, symbol!, clampedDepth, normalizedMaxResults, ct);
                // ExpandAndFormatAsync liefert keine getrennte Truncated-Flag; die eigene
                // Meta-Zeile enthaelt "hard-cap" nur bei tatsaechlicher Kappung (siehe
                // CallGraphTraversal.AggregateAndTruncate) — Marker statt Signatur-Aenderung,
                // um den bestehenden String-Vertrag von ExpandAndFormatAsync nicht zu brechen.
                isTruncated = body.Contains("hard-cap", StringComparison.Ordinal);
            }

            // Sufficiency-Hinweis nur fuer nicht-trunkierte Ergebnisse — ein trunkiertes
            // Ergebnis traegt bereits seine eigene Meta-Zeile ("depth reduzieren oder maxResults
            // erhoehen"), die implizit "weitere Calls noetig" signalisiert.
            var finalBody = isTruncated ? body : McpSufficiencyHints.Append(body);
            var finalText = FindSymbolTool.PrependWarning(warning, finalBody);
            // In ein Objekt gewrappt statt des nackten Arrays — MCP-Clients validieren structuredContent
            // schema-seitig als JSON-Objekt, ein Top-Level-Array liess den Tool-Call fehlschlagen.
            return entries is null
                ? McpToolResults.Text(finalText)
                : McpToolResults.Text(finalText, new { CallSites = entries });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_references: {ex.Message}",
                context: symbolIdentifier);
        }
    }

    /// <summary>
    /// Loest <paramref name="identifier"/> ueber eine stabile DocumentationCommentId, eine
    /// Datei:Zeile:Spalte-Angabe oder einen qualifizierten/teil-qualifizierten Namen zu genau
    /// einem Symbol auf — gemeinsamer Einstiegspunkt fuer alle drei dokumentierten Formate. Reine
    /// Funktion (Solution rein, Symbol/Fehler raus) ohne Abhaengigkeit von
    /// <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Normalisiert Accessor-Symbole
    /// (Property/Event) auf den zugrunde liegenden Owner, damit eine Position auf einem
    /// <c>get</c>/<c>set</c>/<c>add</c>/<c>remove</c>-Keyword konsistent dieselbe ID liefert
    /// wie eine Position auf dem Property-/Event-Namen.
    /// </summary>
    internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        var (symbol, error) = await ResolveSymbolCoreAsync(solution, identifier, ct);
        return (NormalizeToOwningMember(symbol), error);
    }

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolCoreAsync(
        Solution solution, string identifier, CancellationToken ct)
    {
        var (stableSymbol, stableError) =
            await SymbolIdentifierResolver.TryResolveByStableIdAsync(solution, identifier, ct);
        if (stableError is not null) return (null, stableError);
        if (stableSymbol is not null) return (stableSymbol, null);

        if (SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out var column))
        {
            return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
        }

        return await ResolveByNameAsync(solution, identifier, ct);
    }

    private static ISymbol? NormalizeToOwningMember(ISymbol? symbol) =>
        symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol;

    private static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveByPositionAsync(
        Solution solution, string identifier, string path, int line, int column, CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, path));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);
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
