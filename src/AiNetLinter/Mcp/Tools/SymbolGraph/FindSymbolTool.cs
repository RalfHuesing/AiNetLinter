#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindSymbolRequest(
    ISolutionStateProvider State,
    string[]? NamePatterns,
    string? Kind,
    int MaxResults,
    CancellationToken CancellationToken,
    string? NamePattern = null,
    string? Symbol = null);

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

    internal static IReadOnlyList<string> NormalizeNamePatterns(
        string[]? namePatterns,
        string? namePattern = null,
        string? symbol = null)
    {
        var patterns = McpBatchArguments.Normalize(namePatterns);
        if (patterns.Count > 0) return patterns;

        var scalar = string.IsNullOrWhiteSpace(namePattern) ? symbol : namePattern;
        return string.IsNullOrWhiteSpace(scalar) ? patterns : [scalar];
    }

    internal static CallToolResult? ValidateNamePatterns(IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'namePatterns' fehlt oder ist leer.",
                hint: McpToolResults.NamePatternsBatchHint);
        }

        return patterns.Count > MaxPatternsPerCall
            ? McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Maximal {MaxPatternsPerCall} namePatterns pro Call erlaubt (angefordert: {patterns.Count}).",
                hint: "Auf mehrere Calls aufteilen (z. B. 2x 5-10 Patterns).")
            : null;
    }

    internal static CallToolResult? ValidateKind(string? kind) =>
        kind is not null && !ValidKinds.Contains(kind)
            ? McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Unbekannter kind-Filter '{kind}'.",
                hint: "Gueltige Werte: Klasse/class, Methode/method, Interface/interface, Property/property, Record/record.")
            : null;

    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert an den Scanner.
    /// Ein defensiver try/catch-Wrapper faengt unerwartete Roslyn-Exceptions ab und liefert
    /// einen strukturierten [ERROR]-Antwort statt eines Server-Crashs.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string[]? namePatterns,
        string? kind,
        int maxResults,
        CancellationToken ct) =>
        await ExecuteAsync(new FindSymbolRequest(state, namePatterns, kind, maxResults, ct));

    internal static async Task<CallToolResult> ExecuteAsync(FindSymbolRequest request)
    {
        var patterns = NormalizeNamePatterns(request.NamePatterns, request.NamePattern, request.Symbol);
        var validationError = ValidateNamePatterns(patterns) ?? ValidateKind(request.Kind);
        if (validationError is not null) return validationError;

        var normalizedMaxResults = request.MaxResults < 1 ? 1 : request.MaxResults;

        if (request.State.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = request.State.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        try
        {
            var results = new List<FindSymbolPatternResultDto>(patterns.Count);
            var mb = new MarkdownBuilder();

            for (var i = 0; i < patterns.Count; i++)
            {
                request.CancellationToken.ThrowIfCancellationRequested();
                if (i > 0) mb.Divider();
                var pattern = patterns[i];
                var (text, entries) = await FindSymbolScanner.FindMatchesWithEntriesAsync(
                    new FindSymbolScanRequest(
                        solution,
                        pattern,
                        request.Kind,
                        normalizedMaxResults,
                        request.State.AssemblySymbolIdentity),
                    request.CancellationToken);
                results.Add(new FindSymbolPatternResultDto(pattern, entries));

                mb.Heading(3, $"Symbol-Suche: `{pattern}`").BlankLine();
                mb.Line(text.TrimEnd());
            }

            var markdown = mb.Build().TrimEnd();
            return McpToolResults.Text(markdown, new FindSymbolBatchDto(results));
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
    internal static IEnumerable<string> FormatSymbolLocations(
        ISymbol symbol,
        string outputRoot,
        AnalysisSymbolIdentity? assemblyIdentity = null,
        bool absolutePaths = false)
    {
        foreach (var entry in FormatSymbolLocationEntries(symbol, outputRoot, assemblyIdentity, absolutePaths))
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
    internal static IEnumerable<SymbolLocationEntry> FormatSymbolLocationEntries(
        ISymbol symbol,
        string outputRoot,
        AnalysisSymbolIdentity? assemblyIdentity = null,
        bool absolutePaths = false)
    {
        var kindLabel = SymbolKindClassifier.DescribeSymbolKind(symbol);
        var symbolId = DocumentationCommentId.CreateDeclarationId(symbol)
            ?? CallGraphTraversal.GetStableSymbolId(symbol);
        var qualifiedId = assemblyIdentity?.Format(symbolId) ?? symbolId;
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var lineSpan = location.GetLineSpan();
            var sourcePath = location.SourceTree!.FilePath;
            var displayPath = assemblyIdentity is null && !absolutePaths
                ? PathNormalizer.ToRelative(outputRoot, sourcePath)
                : Path.GetFullPath(sourcePath);
            var line = lineSpan.StartLinePosition.Line + 1;
            yield return new SymbolLocationEntry(displayPath, line, kindLabel, symbol.ToDisplayString(), qualifiedId);
        }
    }

    internal static string FormatEntry(SymbolLocationEntry entry)
    {
        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        var id = entry.Id is null ? string.Empty : $" id: `{entry.Id}`";
        return $"{entry.FilePath}:{entry.Line} - {entry.Kind}: {entry.Name}{id}{origin}";
    }

}

/// <summary>
/// StructuredContent-Hülle für <c>find_symbol</c> — enthält die Ergebnisliste aller angefragten Namens-Muster.
/// </summary>
internal sealed record FindSymbolBatchDto(
    IReadOnlyList<FindSymbolPatternResultDto> Results,
    AssemblyNavigationSummary? Navigation = null);

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
internal sealed record SymbolLocationEntry(
    string FilePath,
    int Line,
    string Kind,
    string Name,
    string? Id = null,
    AssemblyNavigationOrigin? Origin = null);
