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
    internal static SearchPatternLegacyFileHitScanResult Scan(
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
        var fileReadErrorCount = 0;
        var regexTimedOut = false;

        foreach (var projectDir in Web.WebFileCatalog.GetProjectDirectories(solution).OrderBy(d => d, StringComparer.Ordinal))
        {
            foreach (var filePath in FileSystemExclusionHelpers.SafeEnumerateFiles(projectDir).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (FileSystemExclusionHelpers.IsGeneratedPath(filePath)) continue;
                var match = FileMatches(filePath, pattern, regex);
                fileReadErrorCount += match.FileReadError ? 1 : 0;
                regexTimedOut |= match.RegexTimedOut;
                if (match.Matches)
                {
                    filesWithHits.Add(Path.GetRelativePath(solutionDir, filePath).Replace('\\', '/'));
                }
            }
        }

        return new(filesWithHits.ToList(), fileReadErrorCount, regexTimedOut);
    }

    internal static IReadOnlyList<string> GetFilesWithHits(
        Solution solution,
        string pattern,
        bool isRegex)
        => Scan(solution, pattern, isRegex).Files;

    private static SearchPatternLegacyFileMatchResult FileMatches(
        string filePath,
        string pattern,
        Regex? regex)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException) { return new(false, true, false); }
        catch (UnauthorizedAccessException) { return new(false, true, false); }
        try
        {
            var matches = lines.Any(line => regex is not null
                ? regex.IsMatch(line)
                : line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            return new(matches, false, false);
        }
        catch (RegexMatchTimeoutException)
        {
            return new(false, false, true);
        }
    }
}
