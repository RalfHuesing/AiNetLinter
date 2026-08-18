#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Symbol-Scan- und Format-Logik fuer <see cref="FindSymbolTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="FindSymbolTool"/>s eigener <c>AIContextFootprint</c>
/// (siehe <c> klein bleibt 
/// <see cref="SearchPatternScanner"/>,. Keine Abhaengigkeit von
/// <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Trunkierung des
/// Haupt-Treffer-Outputs ueber <see cref="McpTruncation.TruncateLines"/>, Trunkierung der
/// Miss-Hint-Datei-Liste ueber <see cref="McpTruncation.TruncateFileList"/>
///.
/// </summary>
internal static class FindSymbolScanner
{
    /// <summary>
    /// Liefert den fertig formatierten und trunkierten Treffer-Text fuer
    /// <paramref name="namePattern"/>. Verwendet <see cref="SymbolFinder"/> fuer die
    /// Symbol-Suche, <see cref="McpTruncation"/> fuer die Trunkierung. Bei null
    /// C#-Treffern wird der Miss-Hint ueber <see cref="SearchPatternScanner.GetFilesWithHits"/>
    /// aufgebaut und ebenfalls trunkiert.
    /// </summary>
    /// <param name="solution">Bereits geladene Roslyn-Solution.</param>
    /// <param name="namePattern">Substring-Match auf Symbol-Namen (case-insensitive).</param>
    /// <param name="kind">Optionaler Kind-Filter ("class"/"interface"/"method"/"property").</param>
    /// <param name="maxResults">Obergrenze fuer die Anzahl ausgegebener Trefferzeilen
    /// (siehe <see cref="McpTruncation.TruncateLines"/>); muss >= 1 sein (Aufrufer normalisiert).</param>
    /// <returns>Plain-Text-Output (Trefferzeilen + optionale Trunkierungs-Meta-Zeile,
    /// optionale Miss-Hint-Zeile mit eigener Trunkierungs-Meta-Zeile).</returns>
    internal static async Task<string> FindMatchesAndFormat(
        Solution solution,
        string namePattern,
        string? kind,
        int maxResults)
    {
        var (text, _) = await FindMatchesWithEntriesAsync(solution, namePattern, kind, maxResults);
        return text;
    }

    /// <summary>
    /// Wie <see cref="FindMatchesAndFormat"/>, liefert zusaetzlich die <see cref="SymbolLocationEntry"/>-
    /// Liste fuer <c>find_symbol</c>s <c>StructuredContent</c> — dieselbe Symbolsuche/Filterung
    /// einmal ausgefuehrt statt dupliziert, <see cref="FindMatchesAndFormat"/> ist ein duenner
    /// Wrapper darauf (bestehende Signatur/bestehendes Verhalten unveraendert, siehe dessen
    /// direkte Tests in FindSymbolScannerTests/FindSymbolToolTests). Die Entries sind auf
    /// <paramref name="maxResults"/> gekappt, konsistent zur Text-Trunkierung.
    /// </summary>
    internal static async Task<(string Text, IReadOnlyList<SymbolLocationEntry> Entries)> FindMatchesWithEntriesAsync(
        Solution solution,
        string namePattern,
        string? kind,
        int maxResults)
    {
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution,
            name => name.Contains(namePattern, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            CancellationToken.None);

        var filtered = FilterByKind(symbols, kind).ToList();
        if (filtered.Count == 0)
        {
            var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
            var baseText = $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
            return (AppendMissHint(solution, namePattern, baseText), Array.Empty<SymbolLocationEntry>());
        }

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var allEntries = filtered.SelectMany(symbol => FindSymbolTool.FormatSymbolLocationEntries(symbol, outputRoot)).ToList();
        var lines = allEntries.Select(e => $"{e.FilePath}:{e.Line} - {e.Kind}: {e.Name}").ToList();
        var text = McpTruncation.TruncateLines(lines, lines.Count, maxResults);
        var shownEntries = allEntries.Count <= maxResults ? allEntries : allEntries.Take(maxResults).ToList();
        return (text, shownEntries);
    }

    private static string AppendMissHint(Solution solution, string namePattern, string baseText)
    {
        var missHits = SearchPatternScanner.GetFilesWithHits(
            solution, namePattern, isRegex: false);
        if (missHits.Count == 0)
        {
            return baseText;
        }
        // Trunkierung der Datei-Liste : Default 10 Dateien, Meta-Zeile via
        // McpTruncation.TruncateFileList. Forward-Slash-Pfade konsistent mit
        // SearchPatternScanner.GetFilesWithHits.
        var fileList = McpTruncation.TruncateFileList(missHits, missHits.Count);
        return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
            $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen).";
    }

    /// <summary>
    /// Wendet den Kind-Filter an. Akzeptiert sowohl die englischen internen Schluesselwoerter als
    /// auch die in <c>Docs/agent-api.md</c> dokumentierten deutschen Werte (identisch zur eigenen
    /// Output-Vokabular "Klasse:"/"Methode:" aus <see cref="FindSymbolTool.FormatSymbolLocations"/>).
    /// <see cref="FindSymbolTool.ExecuteAsync"/> validiert <paramref name="kind"/> vorab gegen
    /// genau dieselbe Wertemenge — ein unbekannter Wert erreicht diese Methode also nie; der
    /// Default-Fall bleibt trotzdem ungefiltert (statt zu werfen), damit die Methode als reine
    /// Funktion auch direkt (z. B. aus Tests) sicher aufrufbar bleibt.
    /// </summary>
    private static IEnumerable<ISymbol> FilterByKind(IEnumerable<ISymbol> symbols, string? kind)
    {
        if (kind is null) return symbols;
        return symbols.Where(s => SymbolKindClassifier.MatchesSymbolKind(s, kind));
    }
}
