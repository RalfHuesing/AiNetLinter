#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools;

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
            return AppendMissHint(solution, namePattern, baseText);
        }

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = filtered.SelectMany(symbol => FindSymbolTool.FormatSymbolLocations(symbol, outputRoot)).ToList();
        return McpTruncation.TruncateLines(lines, lines.Count, maxResults);
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

    private static IEnumerable<ISymbol> FilterByKind(IEnumerable<ISymbol> symbols, string? kind)
    {
        if (kind is null) return symbols;

        return kind.ToLowerInvariant() switch
        {
            "class" => symbols.Where(s => s is ITypeSymbol { TypeKind: TypeKind.Class }),
            "interface" => symbols.Where(s => s is ITypeSymbol { TypeKind: TypeKind.Interface }),
            "method" => symbols.Where(s => s.Kind == SymbolKind.Method),
            "property" => symbols.Where(s => s.Kind == SymbolKind.Property),
            _ => symbols,
        };
    }
}
