#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Symbol-Scan- und Format-Logik fuer <see cref="FindSymbolTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="FindSymbolTool"/>s eigener <c>AIContextFootprint</c>
/// klein bleibt. Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> — direkt unit-testbar.
/// Trunkierung des
/// Haupt-Treffer-Outputs ueber <see cref="McpTruncation.TruncateLines"/>, Trunkierung der
/// Miss-Hint-Datei-Liste ueber <see cref="McpTruncation.TruncateFileList"/>.
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
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var (text, _) = await FindMatchesWithEntriesAsync(request, ct);
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
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            request.Solution,
            name => name.Contains(request.NamePattern, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            ct);

        var filtered = FilterByKind(symbols, request.Kind).ToList();
        if (filtered.Count == 0)
        {
            var kindSuffix = request.Kind is null ? "" : $" (Kind-Filter: {request.Kind})";
            var baseText = $"Keine Treffer fuer '{request.NamePattern}'{kindSuffix}";
            return (AppendMissHint(request.Solution, request.NamePattern, baseText), Array.Empty<SymbolLocationEntry>());
        }

        var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? "";
        var allEntries = filtered
            .SelectMany(symbol => FindSymbolTool.FormatSymbolLocationEntries(symbol, outputRoot, request.AssemblyIdentity))
            .ToList();
        var lines = allEntries.Select(FindSymbolTool.FormatEntry).ToList();
        var text = McpTruncation.TruncateLines(lines, lines.Count, request.MaxResults);
        var shownEntries = allEntries.Count <= request.MaxResults ? allEntries : allEntries.Take(request.MaxResults).ToList();
        return (text, shownEntries);
    }

    private static string AppendMissHint(Solution solution, string namePattern, string baseText)
    {
        var missScan = SearchPatternLegacyFileHitScanner.Scan(
            solution, namePattern, isRegex: false);
        if (missScan.Files.Count == 0 && !missScan.HasErrors)
        {
            return baseText;
        }
        // Trunkierung der Datei-Liste : Default 10 Dateien, Meta-Zeile via
        // McpTruncation.TruncateFileList. Forward-Slash-Pfade konsistent mit
        // SearchPatternScanner.GetFilesWithHits.
        var status = FormatLegacySearchStatus(missScan);
        if (missScan.Files.Count == 0)
        {
            return $"{baseText}\nHinweis: Die Legacy-Textsuche konnte keine Treffer auswerten ({status}).";
        }

        var fileList = McpTruncation.TruncateFileList(missScan.Files, missScan.Files.Count);
        return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
            $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen)." +
            (string.IsNullOrEmpty(status) ? "" : $" {status}");
    }

    private static string FormatLegacySearchStatus(SearchPatternLegacyFileHitScanResult scan)
    {
        var status = new List<string>();
        if (scan.FileReadErrorCount > 0)
        {
            status.Add($"{scan.FileReadErrorCount} Datei(en) konnten nicht gelesen werden");
        }
        if (scan.RegexTimedOut) status.Add("Regex-Timeout");
        return string.Join(", ", status);
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
