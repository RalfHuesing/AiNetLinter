#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AiNetLinter.Baseline;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternLegacyFileHitScanner
{
    internal static IReadOnlyList<string> GetFilesWithHits(
        Solution solution,
        string pattern,
        bool isRegex)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        Regex? regex = isRegex
            ? new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100))
            : null;
        var filesWithHits = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var projectDir in Web.WebFileCatalog.GetProjectDirectories(solution).OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var filePath in FileSystemExclusionHelpers.SafeEnumerateFiles(projectDir).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (FileSystemExclusionHelpers.IsGeneratedPath(filePath)) continue;
                if (FileMatches(filePath, pattern, regex))
                {
                    filesWithHits.Add(Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/'));
                }
            }
        }

        return filesWithHits.ToList();
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
        try
        {
            return lines.Any(line => regex is not null
                ? regex.IsMatch(line)
                : line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
