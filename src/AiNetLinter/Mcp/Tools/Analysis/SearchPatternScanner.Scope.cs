#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static partial class SearchPatternScanner
{
    private static bool IsExcludedByScopeType(string filePath, string? scopeType)
    {
        if (string.IsNullOrWhiteSpace(scopeType) || string.Equals(scopeType, "all", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isTest = AiNetLinter.Core.TestDetector.IsTestFile(filePath);
        if (string.Equals(scopeType, "production", StringComparison.OrdinalIgnoreCase))
        {
            return isTest;
        }

        if (string.Equals(scopeType, "tests", StringComparison.OrdinalIgnoreCase))
        {
            return !isTest;
        }

        return false;
    }

    private static string GetSolutionRoot(Solution solution)
    {
        if (string.IsNullOrWhiteSpace(solution.FilePath))
        {
            throw new ArgumentException("Die Solution benoetigt fuer search_pattern einen Dateipfad.", nameof(solution));
        }

        var root = Path.GetDirectoryName(Path.GetFullPath(solution.FilePath));
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            throw new ArgumentException("Der Solution-Scope ist nicht verfuegbar.", nameof(solution));
        }

        return root;
    }

    private static string NormalizeScope(string? scope, string solutionRoot)
    {
        if (string.IsNullOrWhiteSpace(scope)) return ".";
        ValidateRelativeValue(scope, nameof(scope));
        var fullPath = Path.GetFullPath(Path.Combine(solutionRoot, scope));
        if (!IsWithinRoot(fullPath, solutionRoot))
        {
            throw new ArgumentException("scope muss innerhalb des Solution-Roots liegen.", nameof(scope));
        }

        var relative = Path.GetRelativePath(solutionRoot, fullPath).Replace('\\', '/');
        return string.IsNullOrEmpty(relative) ? "." : relative.Trim('/');
    }

    private static string[] NormalizePatterns(IReadOnlyList<string>? patterns, string parameterName)
    {
        if (patterns is null || patterns.Count == 0) return Array.Empty<string>();
        var normalized = new List<string>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            ValidateRelativeValue(pattern, parameterName);
            normalized.Add(pattern.Replace('\\', '/').Trim('/'));
        }

        return normalized.ToArray();
    }

    private static void ValidateRelativeValue(string value, string parameterName)
    {
        if (Path.IsPathRooted(value) || value.Contains(':', StringComparison.Ordinal)
            || value.Replace('\\', '/').Split('/').Any(segment => segment == ".."))
        {
            throw new ArgumentException($"{parameterName} darf nur solution-relative Werte enthalten.", parameterName);
        }
    }

    private static bool MatchesScope(string relativePath, string scope) =>
        scope == "."
            || relativePath.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesFilters(
        string relativePath,
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes)
    {
        if (includes.Count > 0 && !includes.Any(pattern => MatchesPattern(relativePath, pattern))) return false;
        return !excludes.Any(pattern => MatchesPattern(relativePath, pattern));
    }

    private static bool MatchesPattern(string relativePath, string pattern) =>
        Configuration.FileFilterEvaluator.MatchesGlobForWeb(relativePath, pattern)
            || Configuration.FileFilterEvaluator.MatchesGlobForWeb(Path.GetFileName(relativePath), pattern)
            || (pattern.StartsWith("**/", StringComparison.Ordinal)
                && (Configuration.FileFilterEvaluator.MatchesGlobForWeb(relativePath, pattern[3..])
                    || Configuration.FileFilterEvaluator.MatchesGlobForWeb(Path.GetFileName(relativePath), pattern[3..])));

    private static bool IsMinified(string relativePath) =>
        Path.GetFileName(relativePath).Contains(".min.", StringComparison.OrdinalIgnoreCase);

    private static string? ToRelativePath(string solutionRoot, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!IsWithinRoot(fullPath, solutionRoot)) return null;
        return Path.GetRelativePath(solutionRoot, fullPath).Replace('\\', '/');
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProjectName(Solution solution, string filePath)
    {
        return solution.Projects
            .Where(project => !string.IsNullOrEmpty(project.FilePath))
            .Select(project => new
            {
                project.Name,
                Directory = Path.GetDirectoryName(Path.GetFullPath(project.FilePath!)),
            })
            .Where(project => project.Directory is not null && IsWithinRoot(filePath, project.Directory))
            .OrderByDescending(project => project.Directory!.Length)
            .Select(project => project.Name)
            .FirstOrDefault();
    }
}
