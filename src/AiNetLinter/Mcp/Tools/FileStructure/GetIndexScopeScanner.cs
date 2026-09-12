#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Web;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Reine Zaehl-/Formatierungslogik fuer <see cref="GetIndexScopeTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetIndexScopeTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c> klein bleibt 
/// <see cref="SymbolIdentifierResolver"/>). Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> —
/// direkt unit-testbar. .cs-Zaehlung ueber <see cref="SourceFileCatalog.IsValidDocument"/>,
/// alle weiteren Dateiendungen ueber einen deduplizierten Dateisystem-Scan auf Basis von
/// <see cref="WebFileCatalog.GetProjectDirectories"/>.
/// </summary>
internal static class GetIndexScopeScanner
{
    /// <summary>
    /// Baut die vollstaendige Dateityp-Aufschluesselung fuer <paramref name="solution"/> — Text
    /// plus <see cref="FileTypeBreakdownEntry"/>-Liste für den Renderer.
    /// </summary>
    internal static async System.Threading.Tasks.Task<(string Text, IReadOnlyList<FileTypeBreakdownEntry> Entries, IndexScopePopulation Population)> BuildBreakdownAsync(Solution solution, System.Threading.CancellationToken cancellationToken)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var csCount = CountPhysicalCSharpFiles(solution, solutionDir);
        var nonCSharpCounts = CountNonCSharpFiles(solution);
        var entries = new List<FileTypeBreakdownEntry>();
        if (csCount > 0)
        {
            entries.Add(new FileTypeBreakdownEntry(
                ".cs",
                csCount,
                SymbolGraphCovered: true,
                RoutingTool: "find_symbol",
                QueryField: "pattern",
                ScopeType: null,
                IncludePatterns: null));
        }
        entries.AddRange(nonCSharpCounts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new FileTypeBreakdownEntry(
                pair.Key,
                pair.Value,
                SymbolGraphCovered: false,
                RoutingTool: "search_pattern",
                QueryField: "pattern",
                ScopeType: "all",
                IncludePatterns: [$"**/*{pair.Key}"])));

        var documents = solution.Projects.SelectMany(project => project.Documents).ToList();
        var classifier = new McpScopeClassifier();
        var scopes = await System.Threading.Tasks.Task.WhenAll(documents.Select(document => classifier.ClassifyAsync(document, cancellationToken)));
        var generatedDocumentCount = scopes.Count(scope => scope.SourceKind == McpSourceKind.Generated);
        var testDocumentCount = scopes.Count(scope => scope.ProjectKind == McpProjectKind.Tests);
        var population = new IndexScopePopulation(
            csCount + nonCSharpCounts.Values.Sum(),
            documents.Count,
            generatedDocumentCount,
            testDocumentCount,
            entries.Sum(entry => entry.Count));
        return (FormatBreakdown(entries, population), entries, population);
    }

    private static int CountPhysicalCSharpFiles(Solution solution, string solutionDir)
    {
        var physicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)
                    || string.IsNullOrWhiteSpace(document.FilePath)
                    || !File.Exists(document.FilePath)) continue;

                physicalPaths.Add(Path.GetFullPath(document.FilePath));
            }
        }

        return physicalPaths.Count;
    }

    private static IReadOnlyDictionary<string, int> CountNonCSharpFiles(Solution solution)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var seenAbsolutePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectDir in WebFileCatalog.GetProjectDirectories(solution))
        {
            foreach (var filePath in FileSystemExclusionHelpers.SafeEnumerateFiles(projectDir))
            {
                CountFileExtension(filePath, seenAbsolutePaths, counts);
            }
        }

        CountFileExtension(solution.FilePath, seenAbsolutePaths, counts);
        return counts;
    }

    private static void CountFileExtension(
        string? filePath,
        ISet<string> seenAbsolutePaths,
        IDictionary<string, int> counts)
    {
        if (string.IsNullOrEmpty(filePath)
            || !File.Exists(filePath)
            || FileSystemExclusionHelpers.IsGeneratedPath(filePath)
            || !seenAbsolutePaths.Add(filePath)) return;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || extension == ".cs") return;
        counts[extension] = counts.TryGetValue(extension, out var count) ? count + 1 : 1;
    }

    private static string FormatBreakdown(
        IReadOnlyList<FileTypeBreakdownEntry> entries,
        IndexScopePopulation population) =>
        string.Join("\n", entries.Select(FormatFileCountLine)) +
        $"\n\nPopulation: {population.PhysicalFileCount} physische Dateien in der Aufschluesselung; " +
        $"{population.RoslynDocumentCount} Roslyn-Dokumente " +
        $"({population.GeneratedDocumentCount} generiert, {population.TestDocumentCount} in Testprojekten); " +
        $"{population.ShownPhysicalFileCount} physische Dateien nach Extension gezeigt.";

    private static string FormatFileCountLine(FileTypeBreakdownEntry entry)
    {
        var suffix = entry.SymbolGraphCovered
            ? " (voll vom Symbolgraph abgedeckt)"
            : " (nicht vom Symbolgraph abgedeckt)";
        var patterns = entry.IncludePatterns is not null ? string.Join(", ", entry.IncludePatterns) : "";
        var route = entry.RoutingTool == "find_symbol"
            ? "routing=find_symbol(pattern)"
            : $"routing=search_pattern(pattern, scopeType={entry.ScopeType}, includePatterns={patterns})";
        return FormatFileCountLine(entry.Count, entry.Extension, suffix, route);
    }

    private static string FormatFileCountLine(int count, string extension, string suffix, string route)
    {
        var fileLabel = count == 1 ? "Datei" : "Dateien";
        return $"{extension}: {count} {fileLabel}{suffix} | {route}";
    }

}

/// <summary>
/// Render-Eintrag für <c>get_index_scope</c> — ein Objekt je vorhandener Dateiendung
/// mit Anzahl und ob sie vom Roslyn-Symbolgraph abgedeckt ist (nur <c>.cs</c>; siehe Scope-Hinweis-
/// Text der anderen C#-only-Tools).
/// </summary>
internal sealed record FileTypeBreakdownEntry(
    string Extension,
    int Count,
    bool SymbolGraphCovered,
    string RoutingTool,
    string QueryField,
    string? ScopeType,
    IReadOnlyList<string>? IncludePatterns);

internal sealed record IndexScopePayload(
    IReadOnlyList<FileTypeBreakdownEntry> Breakdown,
    IndexScopePopulation Population,
    string Status,
    IndexScopeRouting Routing);

internal sealed record IndexScopeRouting(
    IndexScopeRoute CSharp,
    IndexScopeRoute NonCSharp);

internal sealed record IndexScopeRoute(
    string Tool,
    string QueryField,
    string? ScopeType,
    IReadOnlyList<string>? IncludePatterns);

internal sealed record IndexScopePopulation(
    int PhysicalFileCount,
    int RoslynDocumentCount,
    int GeneratedDocumentCount,
    int TestDocumentCount,
    int ShownPhysicalFileCount);
