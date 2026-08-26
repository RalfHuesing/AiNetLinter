#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>find_symbol</c>: durchsucht die resident gehaltene Solution per Substring auf
/// Symbolnamen (optionaler Kind-Filter) und liefert Fundstellen (Datei:Zeile, Kind, Signatur).
/// Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph). Trunkiert standardmaessig auf 50 Treffer,
/// ueberschreibbar via <c>maxResults</c>. Argument-Validierung lebt im Tool (nicht im Scanner),
/// damit der Scanner reine Daten bekommt und einfacher unit-testbar bleibt. Bewusst duenner
/// Dispatch auf <see cref="FindSymbolScanner.FindMatchesAndFormat"/> — keine eigene Scan- oder
/// Formatierungslogik, damit diese Klasse klein bleibt.
/// </summary>
internal static class FindSymbolTool
{
    /// <summary>
    /// Gueltige Werte fuer den optionalen <c>kind</c>-Filter — Deutsch (dokumentiertes Format
    /// aus <c>Docs/agent-api.md</c>, identisch zur eigenen Output-Vokabular "Klasse:"/"Methode:")
    /// und Englisch (interne <see cref="FindSymbolScanner.FilterByKind"/>-Schluesselwoerter)
    /// gleichermassen zugelassen, case-insensitive.
    /// </summary>
    private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "klasse", "interface", "method", "methode", "property", "record",
    };

    internal const int MaxPatternsPerCall = 10;

    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert an den Scanner.
    /// Stellt dem Scanner-Output einen Warnhinweis voran, falls die Solution
    /// Compile-Fehler in einzelnen Dateien hat (Roslyn toleriert sie, aber der Agent weiss sonst
    /// nicht, dass die Antwort unvollstaendig sein kann). Defensiver try/catch-Wrapper faengt
    /// unerwartete Roslyn-Exceptions ab und liefert einen strukturierten [ERROR]-Antwort statt
    /// eines Server-Crashs.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string[]? namePatterns,
        string? kind,
        int maxResults,
        CancellationToken ct)
    {
        var patterns = McpBatchArguments.Normalize(namePatterns);
        if (patterns.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'namePatterns' fehlt oder ist leer.",
                hint: McpToolResults.NamePatternsBatchHint);
        }

        if (patterns.Count > MaxPatternsPerCall)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Maximal {MaxPatternsPerCall} namePatterns pro Call erlaubt (angefordert: {patterns.Count}).",
                hint: "Auf mehrere Calls aufteilen (z. B. 2x 5-10 Patterns).");
        }

        if (kind is not null && !ValidKinds.Contains(kind))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Unbekannter kind-Filter '{kind}'.",
                hint: "Gueltige Werte: Klasse/class, Methode/method, Interface/interface, Property/property, Record/record.");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;

        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        try
        {
            var results = new List<FindSymbolPatternResultDto>(patterns.Count);
            var mb = new MarkdownBuilder();

            for (var i = 0; i < patterns.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (i > 0) mb.Divider();
                var pattern = patterns[i];
                var (text, entries) = await FindSymbolScanner.FindMatchesWithEntriesAsync(
                    solution, pattern, kind, normalizedMaxResults, ct);
                results.Add(new FindSymbolPatternResultDto(pattern, entries));

                mb.Heading(3, $"Symbol-Suche: `{pattern}`").BlankLine();
                mb.Line(text.TrimEnd());
            }

            var warning = await BuildAggregateWarningAsync(solution, ct);
            var markdown = mb.Build().TrimEnd();
            return McpToolResults.Text(PrependWarning(warning, markdown), new FindSymbolBatchDto(results));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_symbol: {ex.Message}",
                context: string.Join(", ", patterns));
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
        foreach (var entry in FormatSymbolLocationEntries(symbol, outputRoot))
        {
            yield return FormatEntry(entry);
        }
    }

    /// <summary>
    /// Strukturierte Variante von <see cref="FormatSymbolLocations"/> — eine
    /// <see cref="SymbolLocationEntry"/> je Quell-Fundstelle von <paramref name="symbol"/>.
    /// Einzige Quelle der Wahrheit fuer beide Formen (Text via <see cref="FormatEntry"/>,
    /// JSON via <see cref="FindSymbolScanner.FindMatchesWithEntriesAsync"/>s
    /// <c>StructuredContent</c>), damit Text und JSON nie auseinanderdriften.
    /// </summary>
    internal static IEnumerable<SymbolLocationEntry> FormatSymbolLocationEntries(ISymbol symbol, string outputRoot)
    {
        var kindLabel = SymbolKindClassifier.DescribeSymbolKind(symbol);
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var lineSpan = location.GetLineSpan();
            var relativePath = PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
            var line = lineSpan.StartLinePosition.Line + 1;
            yield return new SymbolLocationEntry(relativePath, line, kindLabel, symbol.ToDisplayString());
        }
    }

    private static string FormatEntry(SymbolLocationEntry entry) =>
        $"{entry.FilePath}:{entry.Line} - {entry.Kind}: {entry.Name}";

    /// <summary>
    /// Baut einen Warnhinweis, falls mindestens eine Datei einen Compile-Fehler hat. Shared-Helper,
    /// weil das identische Muster in
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

/// <summary>
/// StructuredContent-Hülle für <c>find_symbol</c> — enthält die Ergebnisliste aller angefragten Namens-Muster.
/// </summary>
internal sealed record FindSymbolBatchDto(IReadOnlyList<FindSymbolPatternResultDto> Results);

/// <summary>
/// Ein Einzelergebnis für ein angefragtes Namens-Muster in <c>find_symbol</c>.
/// </summary>
internal sealed record FindSymbolPatternResultDto(string NamePattern, IReadOnlyList<SymbolLocationEntry> Matches);

/// <summary>
/// StructuredContent-Eintrag fuer <c>find_symbol</c> — eine Quell-Fundstelle eines Symbols
/// (Pfad, Zeile, Kind, voll qualifizierter Name). Ein Symbol mit mehreren Deklarationen (z. B.
/// <c>partial class</c>) liefert einen Eintrag je Fundstelle, konsistent zu
/// <see cref="FindSymbolTool.FormatSymbolLocations"/>s Text-Zeilen.
/// </summary>
internal sealed record SymbolLocationEntry(string FilePath, int Line, string Kind, string Name);
