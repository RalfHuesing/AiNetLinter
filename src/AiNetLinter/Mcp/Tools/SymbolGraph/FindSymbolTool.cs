#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Validation;
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
    string? Pattern = null,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false,
    McpScopeClassifier? ScopeClassifier = null)
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
/// Dispatch auf <see cref="FindSymbolScanner.FindMatchesWithDetailsAsync"/> — keine eigene Scan- oder
/// Formatierungslogik, damit diese Klasse klein bleibt.
/// </summary>
internal static class FindSymbolTool
{
    internal const int MaxPatternsPerCall = 10;
    internal const int DefaultMaxResponseBytes = 16 * 1024;

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes) =>
        FindSymbolResponseBudget.Apply(result, maxResponseBytes);

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
        if (options.NamePatterns is not null && options.Pattern is not null)
        {
            return McpToolResults.InvalidArgument(
                "namePatterns und pattern sind gegenseitig exklusiv — genau eines angeben.",
                hint: "Entweder namePatterns (Array) ODER pattern (ein String) angeben, nie beide.",
                fieldPath: "$.namePatterns");
        }

        if (options.NamePatterns is null)
        {
            return options.Pattern is null
                ? McpToolResults.InvalidArgument(
                    "Pflichtparameter 'namePatterns' oder 'pattern' fehlt.",
                    McpToolResults.NamePatternsBatchHint,
                    "$.namePatterns")
                : IsEmptyNamePattern(options.Pattern)
                    ? McpToolResults.InvalidArgument(
                        "pattern darf nicht leer sein.",
                        "Ein nicht-leeres Symbolmuster angeben.",
                        "$.pattern")
                    : null;
        }

        if (options.NamePatterns.Length == 0)
        {
            return McpToolResults.InvalidArgument(
                "namePatterns darf nicht leer sein.",
                McpToolResults.NamePatternsBatchHint,
                "$.namePatterns");
        }

        if (options.NamePatterns.Length > MaxPatternsPerCall)
        {
            return McpToolResults.InvalidArgument(
                $"Maximal {MaxPatternsPerCall} namePatterns pro Call erlaubt (angefordert: {options.NamePatterns.Length}).",
                "Auf mehrere Calls aufteilen (z. B. 2x 5-10 Patterns).",
                "$.namePatterns");
        }

        for (var index = 0; index < options.NamePatterns.Length; index++)
        {
            if (!IsEmptyNamePattern(options.NamePatterns[index])) continue;

            return McpToolResults.InvalidArgument(
                "namePatterns darf keine leeren Elemente enthalten.",
                "Jedes Array-Element muss ein nicht-leeres Symbolmuster enthalten.",
                $"$.namePatterns[{index}]");
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
            return McpToolResults.InvalidArgument(
                "Pflichtparameter 'namePatterns' fehlt oder ist leer.",
                McpToolResults.NamePatternsBatchHint,
                "$.namePatterns");
        }

        return patterns.Count > MaxPatternsPerCall
            ? McpToolResults.InvalidArgument(
                $"Maximal {MaxPatternsPerCall} namePatterns pro Call erlaubt (angefordert: {patterns.Count}).",
                "Auf mehrere Calls aufteilen (z. B. 2x 5-10 Patterns).",
                "$.namePatterns")
            : null;
    }

    internal static CallToolResult? ValidateKind(string? kind) =>
        kind is not null && (string.IsNullOrWhiteSpace(kind) || !McpEnumValues.IsFindSymbolKind(kind))
            ? McpToolResults.InvalidArgument(
                $"Unbekannter kind-Filter '{kind}'.",
                $"Gueltige Werte: {McpEnumValues.FindSymbolKindsHint}.",
                "$.kind")
            : null;

    internal static CallToolResult? ValidateMaxResults(int maxResults) =>
        maxResults < 1
            ? McpToolResults.InvalidArgument(
                "maxResults muss mindestens 1 sein.",
                hint: "Eine positive Ganzzahl angeben.",
                fieldPath: "$.maxResults")
            : null;

    internal static (CallToolResult? Error, McpScopeType ScopeType) ValidateScopeType(string? value)
    {
        if (McpScopeTypeValidator.TryParse(value, out var scopeType, out var fieldPath)) return (null, scopeType);

        return (
            McpToolResults.InvalidArgument(
                $"Ungueltiger scopeType-Wert '{value}'.",
                "scopeType='all', 'production' oder 'tests' angeben.",
                fieldPath),
            McpScopeType.All);
    }

    /// <summary>
    /// Tool-Einstiegspunkt: prueft, ob eine Solution geladen ist, und delegiert an den Scanner.
    /// Ein defensiver try/catch-Wrapper faengt unerwartete Roslyn-Exceptions ab und liefert
    /// einen strukturierten [ERROR]-Antwort statt eines Server-Crashs.
    /// </summary>
    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string[]? namePatterns,
        string? kind,
        int maxResults,
        CancellationToken ct) =>
        ExecuteAsync(new FindSymbolRequest(state, namePatterns, kind, maxResults, ct));

    internal static async Task<CallToolResult> ExecuteAsync(FindSymbolRequest request)
    {
        var patternOptions = request.ToPatternOptions();
        var validationError = ValidatePatternArguments(patternOptions);
        if (validationError is not null) return validationError;

        validationError = ValidateMaxResults(request.MaxResults);
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
            return await ExecuteSearchAsync(request, solution, patterns, normalizedMaxResults);
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
    /// den gerenderten Text), damit Auswahl und Darstellung nie auseinanderdriften.
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
                HandoffKind: qualifiedId is null ? null : symbol is INamedTypeSymbol ? "type" : "member",
                Origin: null,
                Locations:
                [
                    new SymbolSourceLocation(
                        displayPath,
                        line,
                        lineSpan.StartLinePosition.Character + 1,
                        "all",
                        "editable"),
                ]);
        }
    }

    private static async Task<CallToolResult> ExecuteSearchAsync(
        FindSymbolRequest request,
        Solution solution,
        IReadOnlyList<string> patterns,
        int maxResults)
    {
        var markdown = new MarkdownBuilder();
        var scopeClassifier = request.ScopeClassifier ?? new McpScopeClassifier();
        var scopedRequest = request with { ScopeClassifier = scopeClassifier };

        for (var i = 0; i < patterns.Count; i++)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            if (i > 0) markdown.Divider();
            var pattern = patterns[i];
            var scan = await FindPatternAsync(scopedRequest, solution, pattern, maxResults);
            AppendPatternMarkdown(markdown, pattern, scan.Text);
        }

        return McpToolResults.Text(markdown.Build().TrimEnd());
    }

    private static Task<FindSymbolScanResult> FindPatternAsync(
        FindSymbolRequest request,
        Solution solution,
        string pattern,
        int maxResults) =>
        FindSymbolScanner.FindMatchesWithDetailsAsync(
            new FindSymbolScanRequest(
                solution,
                pattern,
                request.Kind,
                maxResults,
                request.State.HandoffSymbolIdentity,
                request.ScopeType,
                request.IncludeGenerated,
                request.ScopeClassifier),
            request.CancellationToken);

    private static void AppendPatternMarkdown(MarkdownBuilder markdown, string pattern, string text)
    {
        markdown.Heading(3, $"Symbol-Suche: `{pattern}`").BlankLine();
        markdown.Line(text.TrimEnd());
    }

    internal static string ToWireValue(McpScopeType scopeType) => scopeType switch
    {
        McpScopeType.Production => "production",
        McpScopeType.Tests => "tests",
        _ => "all",
    };

    internal static string FormatEntry(SymbolLocationEntry entry)
    {
        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        var additionalLocations = entry.Locations?
            .Skip(1)
            .Select(location => $"{location.FilePath}:{location.Line}")
            .ToList();
        var locations = additionalLocations is { Count: > 0 }
            ? $"; weitere Deklarationen: {string.Join(", ", additionalLocations)}"
            : string.Empty;
        var handoff = string.IsNullOrWhiteSpace(entry.Id) ? string.Empty : $"; handoffId: `{entry.Id}`";
        return $"{entry.Kind} {entry.Name} — {entry.FilePath}:{entry.Line}{origin}{locations}{handoff}";
    }

}

public sealed record FindSymbolScopeDto(string RequestedType, bool IncludeGenerated);

internal sealed record SymbolSourceLocation(
    string FilePath,
    int Line,
    int Column,
    string ScopeType,
    string SourceKind,
    string? Project = null);

/// <summary>
/// Ergebnis-Eintrag für <c>find_symbol</c>. Ein Symbol mit mehreren Deklarationen
/// (z. B. <c>partial class</c>) bleibt ein Eintrag; <see cref="Locations"/> enthaelt alle
/// sichtbaren, klassifizierten Fundstellen.
/// </summary>
internal sealed record SymbolLocationEntry(
    string FilePath,
    int Line,
    string Kind,
    string Name,
    string? Id = null,
    string? HandoffKind = null,
    AssemblyNavigationOrigin? Origin = null,
    IReadOnlyList<SymbolSourceLocation>? Locations = null);
