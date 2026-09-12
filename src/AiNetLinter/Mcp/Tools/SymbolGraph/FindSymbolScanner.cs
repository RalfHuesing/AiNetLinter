#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
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
    /// Liste für die <c>find_symbol</c>-Antwort.
    /// </summary>
    internal static async Task<(string Text, IReadOnlyList<SymbolLocationEntry> Entries)> FindMatchesWithEntriesAsync(
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var result = await FindMatchesWithDetailsAsync(request, ct).ConfigureAwait(false);
        return (result.Text, result.Entries);
    }

    internal static async Task<FindSymbolScanResult> FindMatchesWithDetailsAsync(
        FindSymbolScanRequest request,
        CancellationToken ct = default)
    {
        var nameFilter = SymbolNameMatcher.CreateDeclarationNameFilter(request.NamePattern);
        var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
            request.Solution,
            nameFilter,
            SymbolFilter.TypeAndMember,
            ct).ConfigureAwait(false);

        var nameMatches = symbols
            .Where(symbol => SymbolNameMatcher.MatchesSymbol(symbol, request.NamePattern))
            .ToList();

        var filtered = FilterByKind(nameMatches, request.Kind).ToList();
        var kindAlternatives = CreateKindAlternatives(nameMatches, request.Kind);

        if (filtered.Count == 0)
        {
            var missMessage = await FormatMissMessageAsync(request, nameMatches, ct).ConfigureAwait(false);
            return new FindSymbolScanResult(missMessage, Array.Empty<SymbolLocationEntry>(), 0, 0, false, [], kindAlternatives);
        }

        var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? string.Empty;
        var allEntries = (await BuildVisibleEntriesAsync(request, filtered, outputRoot, ct).ConfigureAwait(false))
            .OrderBy(entry => GetMatchRank(entry, request.NamePattern))
            .ThenBy(entry => GetProjectRank(entry))
            .ThenBy(entry => GetSourceRank(entry))
            .ThenBy(entry => entry.Locations?.FirstOrDefault()?.FilePath ?? entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Locations?.FirstOrDefault()?.Line ?? entry.Line)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        if (allEntries.Count == 0)
        {
            return new FindSymbolScanResult(
                $"Keine Treffer fuer '{request.NamePattern}' im angeforderten Scope '{FindSymbolTool.ToWireValue(request.ScopeType)}'.",
                [],
                0,
                0,
                false,
                [],
                kindAlternatives);
        }
        var collectedEntries = allEntries.Take(Math.Max(request.MaxResults, 1)).ToList();
        var isTruncated = allEntries.Count > collectedEntries.Count;
        var text = McpTruncation.TruncateLines(
            collectedEntries.Select(FindSymbolTool.FormatEntry).ToList(),
            allEntries.Count,
            request.MaxResults);
        return new FindSymbolScanResult(
            text,
            collectedEntries,
            allEntries.Count,
            collectedEntries.Count,
            isTruncated,
            isTruncated ? ["maxResults"] : [],
            kindAlternatives);
    }

    internal static async Task<IReadOnlyList<SymbolLocationEntry>> BuildVisibleEntriesAsync(
        FindSymbolScanRequest request,
        IReadOnlyList<ISymbol> symbols,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var grouped = GroupSymbols(symbols);
        var entries = new List<SymbolLocationEntry>(grouped.Count);
        foreach (var (symbol, declarations) in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var locations = await CollectVisibleLocationsAsync(
                request,
                declarations,
                outputRoot,
                cancellationToken).ConfigureAwait(false);
            if (locations.Count == 0) continue;
            locations.Sort(CompareLocations);
            entries.Add(CreateEntry(symbol, locations, request.AssemblyIdentity));
        }

        return entries;
    }

    private static Dictionary<ISymbol, List<ISymbol>> GroupSymbols(IReadOnlyList<ISymbol> symbols)
    {
        var grouped = new Dictionary<ISymbol, List<ISymbol>>(SymbolEqualityComparer.Default);
        foreach (var symbol in symbols)
        {
            var key = symbol.OriginalDefinition;
            if (!grouped.TryGetValue(key, out var declarations))
            {
                declarations = [];
                grouped.Add(key, declarations);
            }

            if (!declarations.Contains(symbol, SymbolEqualityComparer.Default)) declarations.Add(symbol);
        }

        return grouped;
    }

    private static async Task<List<SymbolSourceLocation>> CollectVisibleLocationsAsync(
        FindSymbolScanRequest request,
        IReadOnlyList<ISymbol> declarations,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var classifier = request.ScopeClassifier ?? new McpScopeClassifier();
        var locations = new List<SymbolSourceLocation>();
        var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in declarations)
        {
            foreach (var location in declaration.Locations.Where(candidate => candidate.IsInSource))
            {
                var visible = await TryCreateVisibleLocationAsync(
                    request,
                    location,
                    outputRoot,
                    classifier,
                    cancellationToken).ConfigureAwait(false);
                if (visible.Location is null || !seenLocations.Add(visible.Key)) continue;
                locations.Add(visible.Location);
            }
        }

        return locations;
    }

    private static async Task<(string Key, SymbolSourceLocation? Location)> TryCreateVisibleLocationAsync(
        FindSymbolScanRequest request,
        Location location,
        string outputRoot,
        McpScopeClassifier classifier,
        CancellationToken cancellationToken)
    {
        var document = request.Solution.GetDocument(location.SourceTree!);
        if (document is null) return (string.Empty, null);

        var scope = await classifier.ClassifyAsync(document, cancellationToken).ConfigureAwait(false);
        if (!classifier.MatchesScope(scope, request.ScopeType)
            || (scope.SourceKind == McpSourceKind.Generated && !request.IncludeGenerated))
        {
            return (string.Empty, null);
        }

        var lineSpan = location.GetLineSpan();
        var sourcePath = location.SourceTree!.FilePath;
        var displayPath = !string.IsNullOrWhiteSpace(outputRoot)
            ? PathNormalizer.ToRelative(outputRoot, sourcePath)
            : Path.GetFullPath(sourcePath);
        var key = $"{document.Id}|{lineSpan.StartLinePosition.Line}|{lineSpan.StartLinePosition.Character}";
        var scopeType = scope.ProjectKind == McpProjectKind.Tests
            ? "tests"
            : scope.ProjectKind == McpProjectKind.Production
                ? "production"
                : "all";
        var sourceKind = scope.SourceKind == McpSourceKind.Generated ? "generated" : "editable";
        return (key, new SymbolSourceLocation(
            displayPath,
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            scopeType,
            sourceKind,
            document.Project.Name));
    }

    private static SymbolLocationEntry CreateEntry(
        ISymbol symbol,
        IReadOnlyList<SymbolSourceLocation> locations,
        AnalysisSymbolIdentity? assemblyIdentity)
    {
        var handoffId = assemblyIdentity?.FormatHandoff(symbol);
        var first = locations[0];
        return new SymbolLocationEntry(
            first.FilePath,
            first.Line,
            SymbolKindClassifier.DescribeSymbolKind(symbol),
            symbol.ToDisplayString(),
            handoffId,
            HandoffKind: handoffId is null ? null : symbol is INamedTypeSymbol ? "type" : "member",
            Locations: locations);
    }

    private static IReadOnlyList<string>? CreateKindAlternatives(
        IReadOnlyList<ISymbol> symbols,
        string? requestedKind) =>
        requestedKind is null
            ? null
            : symbols
                .Select(SymbolKindClassifier.DescribeSymbolKind)
                .Where(kind => !string.Equals(kind, requestedKind, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static int CompareLocations(SymbolSourceLocation left, SymbolSourceLocation right)
    {
        var path = StringComparer.OrdinalIgnoreCase.Compare(left.FilePath, right.FilePath);
        return path != 0 ? path : left.Line != right.Line ? left.Line.CompareTo(right.Line) : left.Column.CompareTo(right.Column);
    }

    private static int GetProjectRank(SymbolLocationEntry entry) =>
        entry.Locations?.Any(location => location.ScopeType == "production") == true ? 0
            : entry.Locations?.Any(location => location.ScopeType == "tests") == true ? 1 : 2;

    private static int GetSourceRank(SymbolLocationEntry entry) =>
        entry.Locations?.Any(location => location.SourceKind == "editable") == true ? 0 : 1;

    private static int GetMatchRank(SymbolLocationEntry entry, string pattern)
    {
        var clean = SymbolNameMatcher.CleanPattern(pattern);
        var name = entry.Name;
        if (name.Equals(clean, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($".{clean}", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($".{clean}()", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($".{clean}(", StringComparison.OrdinalIgnoreCase)) return 0;
        if (clean.Contains('.')
            && (name.EndsWith($".{clean}", StringComparison.OrdinalIgnoreCase)
                || name.Contains($".{clean}(", StringComparison.OrdinalIgnoreCase))) return 1;
        if (name.StartsWith(clean, StringComparison.OrdinalIgnoreCase)) return 2;
        if (name.Contains(clean, StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static async Task<string> AppendMissHintAsync(
        Solution solution,
        string namePattern,
        string baseText,
        CancellationToken ct)
    {
        var clean = SymbolNameMatcher.CleanPattern(namePattern).Trim('*', '?');
        var missScan = SearchPatternFileHitScanner.Scan(
            solution, string.IsNullOrWhiteSpace(clean) ? namePattern : clean, isRegex: false);

        var suggestions = await SymbolNameMatcher.FindSimilarSymbolNamesAsync(solution, namePattern, ct).ConfigureAwait(false);
        var suggestionText = suggestions.Count > 0
            ? $"\nÄhnliche Symbole im Projekt: {string.Join(", ", suggestions)}"
            : string.Empty;

        if (missScan.Files.Count == 0 && !missScan.HasErrors)
        {
            return baseText + suggestionText;
        }

        var status = FormatSearchStatus(missScan);
        if (missScan.Files.Count == 0)
        {
            return $"{baseText}\nHinweis: Die ergänzende Textsuche konnte keine Treffer auswerten ({status}).{suggestionText}";
        }

        var fileList = McpTruncation.TruncateFileList(missScan.Files, missScan.Files.Count);
        return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
            $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen)." +
            (string.IsNullOrEmpty(status) ? string.Empty : $" {status}") + suggestionText;
    }

    private static string FormatSearchStatus(SearchPatternFileHitScanResult scan)
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

    private static async Task<string> FormatMissMessageAsync(
        FindSymbolScanRequest request,
        IReadOnlyList<ISymbol> nameMatches,
        CancellationToken ct)
    {
        if (nameMatches.Count > 0 && request.Kind is not null)
        {
            var kindsFound = nameMatches
                .Select(SymbolKindClassifier.DescribeSymbolKind)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var kindList = string.Join(", ", kindsFound);
            var sample = nameMatches[0];
            var loc = sample.Locations.FirstOrDefault(l => l.IsInSource);
            var fileInfo = loc?.SourceTree != null ? $" in {Path.GetFileName(loc.SourceTree.FilePath)}" : string.Empty;
            return $"Keine Treffer fuer '{request.NamePattern}' (Kind-Filter: {request.Kind}). Gefundene Symbole mit abweichendem Kind: {kindList}{fileInfo}.";
        }

        var kindSuffix = request.Kind is null ? string.Empty : $" (Kind-Filter: {request.Kind})";
        var missBaseText = $"Keine Treffer fuer '{request.NamePattern}'{kindSuffix}";
        return await AppendMissHintAsync(request.Solution, request.NamePattern, missBaseText, ct).ConfigureAwait(false);
    }
}

internal sealed record FindSymbolScanResult(
    string Text,
    IReadOnlyList<SymbolLocationEntry> Entries,
    int TotalCount,
    int ReturnedCount,
    bool IsTruncated,
    IReadOnlyList<string> TruncatedBy,
    IReadOnlyList<string>? KindAlternatives = null);
