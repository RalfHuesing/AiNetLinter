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

        // Normalisiere Pfad-Trennzeichen
        var normalizedPath = filePath.Replace('\\', '/');
        foreach (var dirPattern in filters.ExcludeDirectoryPatterns)
        {
            if (string.IsNullOrEmpty(dirPattern)) continue;

            // Normalisiere das dirPattern ebenfalls
            var normalizedDir = dirPattern.Replace('\\', '/');

            // 1. Direkter Substring-Match (z.B. bei "obj/" oder "bin/")
            if (normalizedPath.Contains(normalizedDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Segment-basierter Match, um Fehlalarme bei Substrings zu vermeiden (z.B. "obj" darf nicht "object" matchen)
            var cleanDir = normalizedDir.Trim('/');
            if (cleanDir.Length > 0)
            {
                var dirRegex = "(?:^|/)" + Regex.Escape(cleanDir) + "(?:/|$)";
                if (Regex.IsMatch(normalizedPath, dirRegex, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MatchesGlob(string input, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
    }
}
