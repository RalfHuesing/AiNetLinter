#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>find_symbol</c>: durchsucht die resident gehaltene Solution per Substring auf
/// Symbolnamen (optionaler Kind-Filter) und liefert Fundstellen (Datei:Zeile, Kind, Signatur).
/// Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class FindSymbolTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert sonst an die reine
    /// Formatierungslogik <see cref="FindMatchesAsync"/>.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string namePattern, string? kind, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var text = await FindMatchesAsync(solution, namePattern, kind, ct);
        return McpToolResults.Text(text);
    }

    /// <summary>
    /// Reine Funktion (Solution rein, formatierter String raus) ohne Abhaengigkeit von
    /// <see cref="McpCodeGraphServer"/>/MCP-Protokoll — direkt unit-testbar.
    /// </summary>
    internal static async Task<string> FindMatchesAsync(
        Solution solution, string namePattern, string? kind, CancellationToken ct)
    {
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            solution,
            name => name.Contains(namePattern, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            ct);

        var filtered = FilterByKind(symbols, kind).ToList();
        if (filtered.Count == 0)
        {
            var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
            return $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
        }

        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var lines = filtered.SelectMany(symbol => FormatSymbolLocations(symbol, outputRoot));
        return string.Join("\n", lines);
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

    /// <summary>
    /// Formatiert alle Quell-Fundstellen von <paramref name="symbol"/> als "Datei:Zeile - Kind:
    /// Signatur". Wird auch von <see cref="FindReferencesTool"/> fuer die Ambiguitaets-
    /// Fehlermeldung (Liste der Kandidaten) wiederverwendet.
    /// </summary>
    internal static IEnumerable<string> FormatSymbolLocations(ISymbol symbol, string outputRoot)
    {
        var kindLabel = DescribeKind(symbol);
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var lineSpan = location.GetLineSpan();
            var relativePath = PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
            var line = lineSpan.StartLinePosition.Line + 1;
            yield return $"{relativePath}:{line} - {kindLabel}: {symbol.ToDisplayString()}";
        }
    }

    private static string DescribeKind(ISymbol symbol)
    {
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Class }) return "Klasse";
        if (symbol is ITypeSymbol { TypeKind: TypeKind.Interface }) return "Interface";
        if (symbol.Kind == SymbolKind.Method) return "Methode";
        if (symbol.Kind == SymbolKind.Property) return "Property";
        return symbol.Kind.ToString();
    }
}
