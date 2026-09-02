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
/// Trunkierung des Haupt-Treffer-Outputs ueber <see cref="McpTruncation.TruncateLines"/>,
/// Trunkierung der Miss-Hint-Datei-Liste ueber <see cref="McpTruncation.TruncateFileList"/>.
/// </summary>
internal static class FindSymbolScanner
{
    /// <summary>
    /// Liefert den fertig formatierten und trunkierten Treffer-Text fuer
    /// <paramref name="request.NamePattern"/>. Verwendet <see cref="SymbolFinder"/> fuer die
    /// Symbol-Suche, <see cref="McpTruncation"/> fuer die Trunkierung.
    /// </summary>
    internal static async Task<string> FindMatchesAndFormat(
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var (text, _) = await FindMatchesWithEntriesAsync(request, ct).ConfigureAwait(false);
        return text;
    }

    /// <summary>
    /// Wie <see cref="FindMatchesAndFormat"/>, liefert zusaetzlich die <see cref="SymbolLocationEntry"/>-
    /// Liste fuer <c>find_symbol</c>s <c>StructuredContent</c>.
    /// </summary>
    internal static async Task<(string Text, IReadOnlyList<SymbolLocationEntry> Entries)> FindMatchesWithEntriesAsync(
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var nameFilter = SymbolNameMatcher.CreateDeclarationNameFilter(request.NamePattern);
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            request.Solution,
            nameFilter,
            SymbolFilter.TypeAndMember,
            ct).ConfigureAwait(false);

        var filtered = FilterByKind(symbols, request.Kind)
            .Where(symbol => SymbolNameMatcher.MatchesSymbol(symbol, request.NamePattern))
            .ToList();

        if (filtered.Count == 0)
        {
            var kindSuffix = request.Kind is null ? string.Empty : $" (Kind-Filter: {request.Kind})";
            var baseText = $"Keine Treffer fuer '{request.NamePattern}'{kindSuffix}";
            var textWithHint = await AppendMissHintAsync(request.Solution, request.NamePattern, baseText, ct).ConfigureAwait(false);
            return (textWithHint, Array.Empty<SymbolLocationEntry>());
        }

        var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? string.Empty;
        var allEntries = filtered
            .SelectMany(symbol => FindSymbolTool.FormatSymbolLocationEntries(symbol, outputRoot, request.AssemblyIdentity))
            .ToList();
        var lines = allEntries.Select(FindSymbolTool.FormatEntry).ToList();
        var text = McpTruncation.TruncateLines(lines, lines.Count, request.MaxResults);
        var shownEntries = allEntries.Count <= request.MaxResults ? allEntries : allEntries.Take(request.MaxResults).ToList();
        return (text, shownEntries);
    }

    private static async Task<string> AppendMissHintAsync(
        Solution solution,
        string namePattern,
        string baseText,
        CancellationToken ct)
    {
        var clean = SymbolNameMatcher.CleanPattern(namePattern).Trim('*', '?');
        var missScan = SearchPatternLegacyFileHitScanner.Scan(
            solution, string.IsNullOrWhiteSpace(clean) ? namePattern : clean, isRegex: false);

        var suggestions = await SymbolNameMatcher.FindSimilarSymbolNamesAsync(solution, namePattern, ct).ConfigureAwait(false);
        var suggestionText = suggestions.Count > 0
            ? $"\nÄhnliche Symbole im Projekt: {string.Join(", ", suggestions)}"
            : string.Empty;

        if (missScan.Files.Count == 0 && !missScan.HasErrors)
        {
            return baseText + suggestionText;
        }

        var status = FormatLegacySearchStatus(missScan);
        if (missScan.Files.Count == 0)
        {
            return $"{baseText}\nHinweis: Die Legacy-Textsuche konnte keine Treffer auswerten ({status}).{suggestionText}";
        }

        var fileList = McpTruncation.TruncateFileList(missScan.Files, missScan.Files.Count);
        return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
            $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen)." +
            (string.IsNullOrEmpty(status) ? string.Empty : $" {status}") + suggestionText;
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

    private static IEnumerable<ISymbol> FilterByKind(IEnumerable<ISymbol> symbols, string? kind)
    {
        if (kind is null) return symbols;
        return symbols.Where(s => SymbolKindClassifier.MatchesSymbolKind(s, kind));
    }
}
