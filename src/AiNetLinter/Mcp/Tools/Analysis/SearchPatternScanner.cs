#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AiNetLinter.Web;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// Reine Datei-Inhalts-Scan- und Format-Logik fuer <see cref="SearchPatternTool"/> — in eine eigene
/// Datei ausgelagert, damit <see cref="SearchPatternTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c> klein bleibt.
/// Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Datei-Scan
/// sequentiell (<c>Directory.EnumerateFiles</c> pro Projektverzeichnis), Plain-Text-Treffer als
/// "{relativerPfad}:{zeilennummer}: {inhalt}" (Forward-Slashes, konsistent mit
/// <see cref="GetViolationsScanner"/>). Trunkierung ueber <see cref="McpTruncation.TruncateLines"/>.
/// <para>
/// Zusatz-API <see cref="GetFilesWithHits"/> ist der **importierbare Mechanismus fuer
///**: liefert nur die Dateipfad-Liste (kein Text,
/// keine Zeilennummern), damit <c>find_symbol</c> bei C#-Leermenge einen "es gibt aber Treffer
/// in diesen Nicht-C#-Dateien"-Hinweis bauen kann, ohne Text zu duplizieren.
/// </para>
/// </summary>
internal static class SearchPatternScanner
{
    private const RegexOptions CompiledIgnoreCase =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    /// <summary>
    /// Scannt die Projektverzeichnisse der <paramref name="solution"/> zeilenweise nach
    /// <paramref name="pattern"/>. Bei <paramref name="isRegex"/> wird <paramref name="pattern"/>
    /// als Regex interpretiert (IgnoreCase + Compiled + CultureInvariant), sonst als
    /// case-insensitive Substring. Bei ungueltiger Regex-Syntax wird eine
    /// <see cref="ArgumentException"/> geworfen — <see cref="SearchPatternTool"/> faengt sie ab
    /// und liefert <c>McpToolResults.Error(LinterErrorCodes.InvalidArgument, ...)</c>.
    /// </summary>
    /// <param name="solution">Bereits geladene Roslyn-Solution (kein zweites MSBuild-Laden noetig).</param>
    /// <param name="pattern">Such-Pattern (Substring oder Regex, je nach <paramref name="isRegex"/>).</param>
    /// <param name="isRegex"><see langword="true"/>, wenn <paramref name="pattern"/> als Regex
    /// interpretiert werden soll; sonst case-insensitive Substring-Suche.</param>
    /// <param name="maxResults">Obergrenze fuer die Anzahl ausgegebener Trefferzeilen (siehe
    /// <see cref="McpTruncation.TruncateLines"/>); muss >= 1 sein (Aufrufer validiert).</param>
    /// <returns>Plain-Text-Output (Trefferzeilen + optionale Trunkierungs-Meta-Zeile).</returns>
    /// <exception cref="ArgumentException">Bei ungueltiger Regex-Syntax (nur <paramref name="isRegex"/>=true).</exception>
    internal static string SearchAndFormat(
        Solution solution,
        string pattern,
        bool isRegex,
        int maxResults)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        Regex? regex = isRegex ? new Regex(pattern, CompiledIgnoreCase) : null;

        var hitLines = new List<string>();
        var totalMatches = 0;

        foreach (var projectDir in WebFileCatalog.GetProjectDirectories(solution).OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var filePath in SafeEnumerateFiles(projectDir).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (IsGeneratedPath(filePath)) continue;

                var relativePath = Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/');
                CollectFileHits(filePath, relativePath, pattern, regex, hitLines, ref totalMatches);
            }
        }

        if (totalMatches == 0)
        {
            return "0 Treffer fuer das angegebene Pattern.";
        }

        return McpTruncation.TruncateLines(hitLines, totalMatches, maxResults);
    }

    /// <summary>
    /// Liefert die Liste der Dateipfade (solution-relativ, Forward-Slashes) mit mindestens einem
    /// Treffer fuer <paramref name="pattern"/>. Wird von
    /// <c>find_symbol</c>) verwendet, um bei C#-Leermenge einen "es gibt aber Treffer in diesen
    /// Nicht-C#-Dateien"-Hinweis zu bauen. Keine Sortierung nach Treffer-Haeufigkeit — die Reihenfolge
    /// ist deterministisch nach Dateipfad (ordinal). Wirft <see cref="ArgumentException"/> bei
    /// ungueltiger Regex-Syntax (analog <see cref="SearchAndFormat"/>).
    /// </summary>
    internal static IReadOnlyList<string> GetFilesWithHits(
        Solution solution,
        string pattern,
        bool isRegex)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        Regex? regex = isRegex ? new Regex(pattern, CompiledIgnoreCase) : null;

        var filesWithHits = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var projectDir in WebFileCatalog.GetProjectDirectories(solution).OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var filePath in SafeEnumerateFiles(projectDir).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (IsGeneratedPath(filePath)) continue;
                if (FileMatches(filePath, pattern, regex))
                {
                    var relativePath = Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/');
                    filesWithHits.Add(relativePath);
                }
            }
        }

        return filesWithHits.ToList();
    }

    private static void CollectFileHits(
        string filePath,
        string relativePath,
        string pattern,
        Regex? regex,
        List<string> hitLines,
        ref int totalMatches)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (IsMatch(line, pattern, regex))
            {
                hitLines.Add($"{relativePath}:{i + 1}: {line}");
                totalMatches++;
            }
        }
    }

    private static bool FileMatches(string filePath, string pattern, Regex? regex)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

        return lines.Any(line => IsMatch(line, pattern, regex));
    }

    private static bool IsMatch(string line, string pattern, Regex? regex)
    {
        return regex is not null
            ? regex.IsMatch(line)
            : line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
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

    private static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase);
    }
}
