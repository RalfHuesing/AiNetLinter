#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Web;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Reine Zaehl-/Formatierungslogik fuer <see cref="GetIndexScopeTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetIndexScopeTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c> klein bleibt 
/// <see cref="SymbolIdentifierResolver"/>). Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> —
/// direkt unit-testbar. .cs-Zaehlung ueber <see cref="SourceFileCatalog.IsValidDocument"/>,
/// .css/.js/.razor-Zaehlung ueber <see cref="WebFileCatalog.Collect"/>, .xaml/.html ueber einen neuen,
/// minimalen Dateisystem-Scan auf Basis von <see cref="WebFileCatalog.GetProjectDirectories"/>.
/// </summary>
internal static class GetIndexScopeScanner
{
    /// <summary>
    /// Baut die vollstaendige Dateityp-Aufschluesselung fuer <paramref name="solution"/> als Text.
    /// </summary>
    internal static string BuildBreakdownText(Solution solution)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var csCount = CountCsFiles(solution, solutionDir);
        var webCounts = CountWebFiles(solution, solutionDir);
        var (xamlCount, htmlCount) = CountXamlAndHtmlFiles(solution);

        return FormatBreakdown(csCount, webCounts, xamlCount, htmlCount);
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

    private static IReadOnlyDictionary<WebFileType, int> CountWebFiles(Solution solution, string solutionDir)
    {
        var request = new WebFileDiscoveryRequest(new FileFiltersConfig(), Array.Empty<string>(), null);
        var entries = WebFileCatalog.Collect(solution, solutionDir, request);
        return entries.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count());
    }

    private static (int XamlCount, int HtmlCount) CountXamlAndHtmlFiles(Solution solution)
    {
        var xamlCount = 0;
        var htmlCount = 0;

        foreach (var projectDir in WebFileCatalog.GetProjectDirectories(solution))
        {
            foreach (var filePath in FileSystemExclusionHelpers.SafeEnumerateFiles(projectDir))
            {
                if (FileSystemExclusionHelpers.IsGeneratedPath(filePath)) continue;

                if (filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) xamlCount++;
                else if (filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) htmlCount++;
            }
        }

        return (xamlCount, htmlCount);
    }

    private static string FormatBreakdown(
        int csCount, IReadOnlyDictionary<WebFileType, int> webCounts, int xamlCount, int htmlCount)
    {
        var cssCount = webCounts.GetValueOrDefault(WebFileType.Css);
        var jsCount = webCounts.GetValueOrDefault(WebFileType.Js);
        var razorCount = webCounts.GetValueOrDefault(WebFileType.Razor);

        var lines = new[]
        {
            FormatFileCountLine(csCount, ".cs", " (voll vom Symbolgraph abgedeckt)"),
            FormatFileCountLine(cssCount, ".css", " (nicht vom Symbolgraph abgedeckt)"),
            FormatFileCountLine(htmlCount, ".html", " (nicht vom Symbolgraph abgedeckt)"),
            FormatFileCountLine(jsCount, ".js", " (nicht vom Symbolgraph abgedeckt)"),
            FormatFileCountLine(razorCount, ".razor", " (nicht vom Symbolgraph abgedeckt)"),
            FormatFileCountLine(xamlCount, ".xaml", " (nicht vom Symbolgraph abgedeckt)"),
        };

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Pluralisierung in eine eigene Zeile extrahiert, damit Singular ("1 Datei") und Plural
    /// ("N Dateien") konsistent zu <see cref="McpCompileDiagnostics.FormatAggregateWarning"/>
    /// bleiben und Test-Assertions Singular/Plural gleichermassen matchen koennen.
    /// </summary>
    private static string FormatFileCountLine(int count, string extension, string suffix)
    {
        var fileLabel = count == 1 ? "Datei" : "Dateien";
        return $"{extension}: {count} {fileLabel}{suffix}";
    }
}
