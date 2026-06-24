#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Web;

/// <summary>
/// Repräsentiert eine im Dateisystem gefundene Web-Datei (CSS, JS oder Razor).
/// Spiegel der Architektur aus Research/Extend-Web-Features/00_Overview.md Abschnitt 2.2.
/// </summary>
public sealed record WebFileEntry(string AbsolutePath, string RelativePath, WebFileType Type);

/// <summary>
/// Dateityp einer Web-Asset-Datei.
/// </summary>
public enum WebFileType
{
    Css,
    Js,
    Razor,
}

/// <summary>
/// Enumeriert Web-Dateien aus den Projektverzeichnissen der Solution.
/// Filtert generierte Verzeichnisse (obj/, bin/, node_modules/) und ExemptPaths heraus.
/// Arbeitet auf dem Dateisystem (Roslyn sieht keine .css/.js/.razor-Dateien).
/// </summary>
internal static class WebFileCatalog
{
    /// <summary>
    /// Sammelt alle Web-Dateien aus den Projektverzeichnissen.
    /// </summary>
    /// <param name="solution">Bereits geladene Roslyn-Solution (kein zweites MSBuild-Laden noetig).</param>
    /// <param name="solutionDir">Absoluter Pfad zum Solution-Wurzelverzeichnis.</param>
    /// <param name="fileFilters">Globale FileFilters (ExcludeDirectoryPatterns wird zusaetzlich angewandt).</param>
    /// <param name="cssExemptPaths">CSS-spezifische ExemptPaths aus WebConfig.Css.</param>
    public static IReadOnlyList<WebFileEntry> Collect(
        Solution solution,
        string solutionDir,
        FileFiltersConfig fileFilters,
        IReadOnlyCollection<string> cssExemptPaths)
    {
        if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir))
        {
            return Array.Empty<WebFileEntry>();
        }

        var entries = new List<WebFileEntry>();
        var seenAbsolutePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectDir in GetProjectDirectories(solution))
        {
            CollectFromDirectory(projectDir, solutionDir, fileFilters, cssExemptPaths, entries, seenAbsolutePaths);
        }

        return entries;
    }

    private static IEnumerable<string> GetProjectDirectories(Solution solution)
    {
        return solution.Projects
            .Where(p => !string.IsNullOrEmpty(p.FilePath))
            .Select(p => Path.GetDirectoryName(p.FilePath)!)
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void CollectFromDirectory(
        string projectDir,
        string solutionDir,
        FileFiltersConfig fileFilters,
        IReadOnlyCollection<string> cssExemptPaths,
        List<WebFileEntry> entries,
        HashSet<string> seenAbsolutePaths)
    {
        foreach (var filePath in SafeEnumerateFiles(projectDir))
        {
            if (!TryClassifyFile(filePath, fileFilters, cssExemptPaths, solutionDir,
                    out var type, out var relativePath))
            {
                continue;
            }

            if (!seenAbsolutePaths.Add(filePath)) continue;
            entries.Add(new WebFileEntry(filePath, relativePath, type));
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string projectDir)
    {
        try
        {
            return Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    private static bool TryClassifyFile(
        string filePath,
        FileFiltersConfig fileFilters,
        IReadOnlyCollection<string> cssExemptPaths,
        string solutionDir,
        out WebFileType type,
        out string relativePath)
    {
        type = default!;
        relativePath = string.Empty;

        if (IsGeneratedPath(filePath)) return false;
        if (FileFilterEvaluator.IsExcluded(filePath, fileFilters)) return false;

        var detected = GetWebFileType(filePath);
        if (detected == null) return false;

        relativePath = Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/');

        if (detected == WebFileType.Css && MatchesAnyGlob(relativePath, cssExemptPaths))
        {
            return false;
        }

        type = detected.Value;
        return true;
    }

    private static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase);
    }

    private static WebFileType? GetWebFileType(string path)
    {
        // Vergleich gegen volle Extension — ".razor.css" muss ".css" matchen, nicht ".razor.css".
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
        {
            return WebFileType.Css; // Wird als Scoped CSS behandelt.
        }
        if (fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return WebFileType.Css;
        }
        if (fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return WebFileType.Js;
        }
        if (fileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return WebFileType.Razor;
        }
        return null;
    }

    private static bool MatchesAnyGlob(string relativePath, IReadOnlyCollection<string> patterns)
    {
        if (patterns == null || patterns.Count == 0) return false;
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern)) continue;
            if (FileFilterEvaluator.MatchesGlobForWeb(relativePath, pattern)) return true;
        }
        return false;
    }
}