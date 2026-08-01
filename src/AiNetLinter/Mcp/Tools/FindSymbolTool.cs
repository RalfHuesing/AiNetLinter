#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>find_symbol</c>: durchsucht die resident gehaltene Solution per Substring auf
/// Symbolnamen (optionaler Kind-Filter) und liefert Fundstellen (Datei:Zeile, Kind, Signatur).
/// Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph). Trunkiert standardmaessig auf 50 Treffer,
/// ueberschreibbar via <c>maxResults</c>. Argument-Validierung lebt im Tool (nicht im Scanner),
/// damit der Scanner reine Daten bekommt und einfacher unit-testbar bleibt. Bewusst duenner
/// Dispatch auf <see cref="FindSymbolScanner.FindMatchesAndFormat"/> — keine eigene Scan- oder
/// Formatierungslogik (TD-005-Muster, analog <see cref="SearchPatternTool"/>), damit diese
/// Klasse eigener <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) klein bleibt
/// (TD-012-Scanner-Split).
/// </summary>
internal static class FindSymbolTool
{
    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert an den Scanner.
    /// Stellt dem Scanner-Output einen EPIC-06-Aggregat-Warnhinweis voran, falls die Solution
    /// Compile-Fehler in einzelnen Dateien hat (Roslyn toleriert sie, aber der Agent weiss sonst
    /// nicht, dass die Antwort unvollstaendig sein kann). Defensiver try/catch-Wrapper faengt
    /// unerwartete Roslyn-Exceptions ab und liefert einen strukturierten [ERROR]-Antwort statt
    /// eines Server-Crashs (EPIC-06 Defensiv-Pfad).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string namePattern,
        string? kind,
        int maxResults,
        CancellationToken ct)
    {
        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;

        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        try
        {
            var text = await FindSymbolScanner.FindMatchesAndFormat(
                solution, namePattern, kind, normalizedMaxResults);
            var warning = await BuildAggregateWarningAsync(solution, ct);
            return McpToolResults.Text(PrependWarning(warning, text));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_symbol: {ex.Message}",
                context: namePattern);
        }
    }

    /// <summary>
    /// Formatiert alle Quell-Fundstellen von <paramref name="symbol"/> als "Datei:Zeile - Kind:
    /// Signatur". Wird auch von <see cref="FindReferencesTool"/> fuer die Ambiguitaets-
    /// Fehlermeldung (Liste der Kandidaten) wiederverwendet. Bewusst im Tool (nicht im Scanner)
    /// geblieben, weil es eine tool-uebergreifend genutzte Format-Methode ist und nicht zur
    /// Scanner-Kernlogik gehoert (Konsument sitzt in einem anderen Tool).
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

    /// <summary>
    /// Stellt einen EPIC-06-Aggregat-Warnhinweis vor <paramref name="text"/>, falls die Solution
    /// Compile-Fehler in mindestens einer Datei hat. Shared-Helper, weil das identische Muster in
    /// mehreren Tools verwendet wird (find_symbol, find_references, get_impact,
    /// get_type_hierarchy, search_pattern). Bei 0 Compile-Fehlern wird der Original-Text
    /// unveraendert zurueckgegeben.
    /// </summary>
    internal static async Task<string> BuildAggregateWarningAsync(Solution solution, CancellationToken ct)
    {
        var diagnosticsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct);
        var totalErrors = diagnosticsByFile.Values.Sum(list => list.Count);
        return totalErrors > 0
            ? McpCompileDiagnostics.FormatAggregateWarning(diagnosticsByFile.Count, totalErrors)
            : string.Empty;
    }

    internal static string PrependWarning(string warning, string text)
    {
        return string.IsNullOrEmpty(warning) ? text : warning + "\n\n" + text;
    }
}
