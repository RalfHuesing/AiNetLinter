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
    /// plus <see cref="FileTypeBreakdownEntry"/>-Liste fuer <c>StructuredContent</c>.
    /// </summary>
    internal static async System.Threading.Tasks.Task<(string Text, IReadOnlyList<FileTypeBreakdownEntry> Entries, IndexScopePopulation Population)> BuildBreakdownAsync(Solution solution, System.Threading.CancellationToken cancellationToken)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var csCount = CountCsFiles(solution, solutionDir);
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
                FileFilter: null));
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
                FileFilter: $"**/*{pair.Key}")));

        var text = FormatBreakdown(entries);
        var documents = solution.Projects.SelectMany(project => project.Documents).ToList();
        var classifier = new McpScopeClassifier();
        var scopes = await System.Threading.Tasks.Task.WhenAll(documents.Select(document => classifier.ClassifyAsync(document, cancellationToken)));
        var generatedDocumentCount = scopes.Count(scope => scope.SourceKind == McpSourceKind.Generated);
        var testDocumentCount = scopes.Count(scope => scope.ProjectKind == McpProjectKind.Tests);
        return (text, entries, new IndexScopePopulation(
            csCount + nonCSharpCounts.Values.Sum(),
            documents.Count,
            generatedDocumentCount,
            testDocumentCount,
            documents.Count - csCount,
            entries.Sum(entry => entry.Count)));
    }

    private static int CountCsFiles(Solution solution, string solutionDir)
    {
        var count = 0;
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (SourceFileCatalog.IsValidDocument(document, solutionDir)) count++;
            }
        }

        return count;
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

    private static string FormatBreakdown(IReadOnlyList<FileTypeBreakdownEntry> entries) =>
        string.Join("\n", entries.Select(FormatFileCountLine));

    private static string FormatFileCountLine(FileTypeBreakdownEntry entry)
    {
        var suffix = entry.SymbolGraphCovered
            ? " (voll vom Symbolgraph abgedeckt)"
            : " (nicht vom Symbolgraph abgedeckt)";
        var route = entry.RoutingTool == "find_symbol"
            ? "routing=find_symbol(pattern)"
            : $"routing=search_pattern(pattern, scopeType={entry.ScopeType}, includePatterns={entry.FileFilter})";
        return FormatFileCountLine(entry.Count, entry.Extension, suffix, route);
    }

    private static string FormatFileCountLine(int count, string extension, string suffix, string route)
    {
        var fileLabel = count == 1 ? "Datei" : "Dateien";
        return $"{extension}: {count} {fileLabel}{suffix} | {route}";
    }

}

/// <summary>
/// StructuredContent-Eintrag fuer <c>get_index_scope</c> — ein Objekt je vorhandener Dateiendung
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
    string? FileFilter);

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
    string? FileFilter);

internal sealed record IndexScopePopulation(
    int PhysicalFileCount,
    int RoslynDocumentCount,
    int GeneratedDocumentCount,
    int TestDocumentCount,
    int ExcludedCount,
    int ShownCount);
