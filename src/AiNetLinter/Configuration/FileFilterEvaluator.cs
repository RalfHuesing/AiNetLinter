#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AiNetLinter.Configuration;

/// <summary>
/// Evaluiert, ob eine Datei aufgrund der konfigurierten Datei- und Verzeichnisausschlüsse übersprungen werden soll.
/// </summary>
internal static class FileFilterEvaluator
{
    public static bool IsExcluded(string filePath, FileFiltersConfig filters)
    {
        if (string.IsNullOrEmpty(filePath)) return false;

        var fileName = Path.GetFileName(filePath);

        foreach (var pattern in filters.ExcludeFilePatterns)
        {
            if (string.IsNullOrEmpty(pattern)) continue;
            if (MatchesGlob(fileName, pattern)) return true;
        }

        var normalizedPath = filePath.Replace('\\', '/');
        foreach (var dirPattern in filters.ExcludeDirectoryPatterns)
        {
            if (string.IsNullOrEmpty(dirPattern)) continue;
            if (MatchesDirectoryPattern(normalizedPath, dirPattern)) return true;
        }

        return false;
    }

    private static bool MatchesDirectoryPattern(string normalizedPath, string dirPattern)
    {
        var normalizedDir = dirPattern.Replace('\\', '/');

        if (normalizedPath.Contains(normalizedDir, StringComparison.OrdinalIgnoreCase))
            return true;

        var cleanDir = normalizedDir.Trim('/');
        if (cleanDir.Length == 0) return false;

        var dirRegex = "(?:^|/)" + Regex.Escape(cleanDir) + "(?:/|$)";
        return Regex.IsMatch(normalizedPath, dirRegex, RegexOptions.IgnoreCase);
    }

    private static bool MatchesGlob(string input, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
    }
}
