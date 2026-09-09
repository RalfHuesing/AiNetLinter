#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record FindSymbolPatternOptions(
    string[]? NamePatterns = null,
    string? Pattern = null);

internal sealed record FindSymbolRequest(
    ISolutionStateProvider State,
    string[]? NamePatterns,
    string? Kind,
    int MaxResults,
    CancellationToken CancellationToken,
    string? Pattern = null)
{
    internal FindSymbolPatternOptions ToPatternOptions() =>
        new(NamePatterns, Pattern);
}

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
    /// Gueltige Werte fuer den optionalen <c>kind</c>-Filter — kanonische C#/Roslyn-Bezeichner,
    /// case-insensitive.
    /// </summary>
    private static readonly HashSet<string> ValidKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "interface", "record", "record class", "record struct", "struct", "enum", "delegate", "method", "property",
    };

    internal const int MaxPatternsPerCall = 10;

    internal static IReadOnlyList<string> NormalizeNamePatterns(FindSymbolPatternOptions options)
    {
        var patterns = McpBatchArguments.Normalize(options.NamePatterns);
        if (patterns.Count > 0)
        {
            return patterns
                .Select(McpInputNormalizer.NormalizeSymbolIdentifier)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }

        var scalar = options.Pattern;

        if (string.IsNullOrWhiteSpace(scalar)) return patterns;
        var cleaned = McpInputNormalizer.NormalizeSymbolIdentifier(scalar);
        return string.IsNullOrWhiteSpace(cleaned) ? patterns : [cleaned];
    }

    internal static IReadOnlyList<string> NormalizeNamePatterns(string[]? namePatterns, string? scalar = null) =>
        NormalizeNamePatterns(new FindSymbolPatternOptions(namePatterns, scalar));

    internal static CallToolResult? ValidatePatternArguments(FindSymbolPatternOptions options)
    {
        if (options.NamePatterns is not null && !string.IsNullOrWhiteSpace(options.Pattern))
        {
            return McpToolResults.InvalidArgument(
                "namePatterns und pattern sind gegenseitig exklusiv — genau eines angeben.",
                hint: "Entweder namePatterns (Array) ODER pattern (ein String) angeben, nie beide.");
        }

        if (options.NamePatterns is not null
            && options.NamePatterns.Any(IsEmptyNamePattern)
            && options.NamePatterns.Any(pattern => !IsEmptyNamePattern(pattern)))
        {
            return McpToolResults.InvalidArgument(
                "namePatterns darf keine leeren Elemente enthalten.",
                hint: "Jedes Array-Element muss ein nicht-leeres Symbolmuster enthalten.",
                fieldPath: "namePatterns");
        }

        return null;
    }

    private static bool IsEmptyNamePattern(string? pattern) =>
        string.IsNullOrWhiteSpace(pattern)
        || string.IsNullOrWhiteSpace(McpInputNormalizer.NormalizeSymbolIdentifier(pattern));

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
                hint: "Gueltige Werte: class, method, interface, property, record, struct, enum, delegate.")
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
        var patternOptions = request.ToPatternOptions();
        var validationError = ValidatePatternArguments(patternOptions);
        if (validationError is not null) return validationError;

        var patterns = NormalizeNamePatterns(patternOptions);
        validationError = ValidateNamePatterns(patterns) ?? ValidateKind(request.Kind);
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
                var scan = await FindSymbolScanner.FindMatchesWithDetailsAsync(
                    new FindSymbolScanRequest(
                        solution,
                        pattern,
                        request.Kind,
                        normalizedMaxResults,
                        request.State.HandoffSymbolIdentity),
                    request.CancellationToken);
                results.Add(new FindSymbolPatternResultDto(
                    pattern,
                    scan.Entries,
                    scan.TotalCount,
                    scan.ReturnedCount,
                    scan.IsTruncated,
                    scan.TruncatedBy));

                mb.Heading(3, $"Symbol-Suche: `{pattern}`").BlankLine();
                mb.Line(scan.Text.TrimEnd());
            }

            var markdown = mb.Build().TrimEnd();
            var totalCount = results.Sum(result => result.TotalCount);
            var returnedCount = results.Sum(result => result.ReturnedCount);
            var truncatedBy = results
                .SelectMany(result => result.TruncatedBy ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return McpToolResults.Text(
                markdown,
                new FindSymbolBatchDto(
                    results,
                    TotalCount: totalCount,
                    ReturnedCount: returnedCount,
                    IsTruncated: truncatedBy.Count > 0,
                    TruncatedBy: truncatedBy));
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
        var qualifiedId = assemblyIdentity?.FormatHandoff(symbol);
        foreach (var location in symbol.Locations.Where(l => l.IsInSource))
        {
            var lineSpan = location.GetLineSpan();
            var sourcePath = location.SourceTree!.FilePath;
            var displayPath = !absolutePaths && !string.IsNullOrWhiteSpace(outputRoot)
                ? PathNormalizer.ToRelative(outputRoot, sourcePath)
                : Path.GetFullPath(sourcePath);
            var line = lineSpan.StartLinePosition.Line + 1;
            yield return new SymbolLocationEntry(
                displayPath,
                line,
                kindLabel,
                symbol.ToDisplayString(),
                qualifiedId,
                Handoff: qualifiedId is not null,
                TargetPath: assemblyIdentity?.CanonicalPath,
                Snapshot: assemblyIdentity?.ContentHash,
                AllowedFollowUpTools: qualifiedId is null ? [] : HandoffFollowUpTools.For(symbol));
        }
    }

    internal static string FormatEntry(SymbolLocationEntry entry)
    {
        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        var id = entry.Handoff && entry.Id is not null ? $" id: `{entry.Id}`" : " handoff=false";
        return $"{entry.FilePath}:{entry.Line} - {entry.Kind}: {entry.Name}{id}{origin}";
    }

}

/// <summary>
/// StructuredContent-Hülle für <c>find_symbol</c> — enthält die Ergebnisliste aller angefragten Namens-Muster.
/// </summary>
internal sealed record FindSymbolBatchDto(
    IReadOnlyList<FindSymbolPatternResultDto> Results,
    AssemblyNavigationSummary? Navigation = null,
    int TotalCount = 0,
    int ReturnedCount = 0,
    bool IsTruncated = false,
    IReadOnlyList<string>? TruncatedBy = null);

/// <summary>
/// Ein Einzelergebnis für ein angefragtes Namens-Muster in <c>find_symbol</c>.
/// </summary>
internal sealed record FindSymbolPatternResultDto(
    string NamePattern,
    IReadOnlyList<SymbolLocationEntry> Matches,
    int TotalCount = 0,
    int ReturnedCount = 0,
    bool IsTruncated = false,
    IReadOnlyList<string>? TruncatedBy = null);

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
    AssemblyNavigationOrigin? Origin = null,
    bool Handoff = false,
    string? TargetPath = null,
    string? Snapshot = null,
    IReadOnlyList<string>? AllowedFollowUpTools = null);
