#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.Documents;

/// <summary>
/// Loest Dokumentpfade fuer physische und generierte Roslyn-Dokumente auf.
/// </summary>
internal static class SolutionDocumentPathResolver
{
    internal static Document? Find(Solution solution, string filePath)
    {
        var matches = FindCandidates(solution, filePath);
        return matches.Count == 1 ? matches[0] : null;
    }

    internal static IReadOnlyList<Document> FindCandidates(Solution solution, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return [];

        var pathMatches = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => PathsEquivalent(solution, document.FilePath, filePath))
            .ToList();
        if (pathMatches.Count > 0) return pathMatches;

        var virtualMatches = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => string.Equals(
                NormalizeVirtualPath(document.FilePath),
                NormalizeVirtualPath(filePath),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (virtualMatches.Count > 0) return virtualMatches;

        return IsBareFileName(filePath)
            ? solution.Projects
                .SelectMany(project => project.Documents)
                .Where(document => string.Equals(document.Name, filePath, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [];
    }

    private static bool PathsEquivalent(Solution solution, string? left, string right)
    {
        var leftPaths = GetPathVariants(solution, left).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetPathVariants(solution, right).Any(leftPaths.Contains);
    }

    private static IEnumerable<string> GetPathVariants(Solution solution, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) yield break;

        if (Path.IsPathFullyQualified(path))
        {
            if (TryGetFullPath(path, out var fullPath)) yield return fullPath;
            yield break;
        }

        var solutionDir = GetSolutionDirectory(solution);
        if (solutionDir is null) yield break;
        if (TryGetFullPath(Path.Combine(solutionDir, path), out var relativeFullPath))
        {
            yield return relativeFullPath;
        }
    }

    private static string? GetSolutionDirectory(Solution solution)
    {
        if (string.IsNullOrWhiteSpace(solution.FilePath)
            || !Path.IsPathFullyQualified(solution.FilePath))
        {
            return null;
        }

        return Path.GetDirectoryName(solution.FilePath);
    }

    private static string NormalizeVirtualPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        while (normalized.StartsWith($".{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool IsBareFileName(string path) =>
        !path.Contains(Path.DirectorySeparatorChar) && !path.Contains(Path.AltDirectorySeparatorChar);
}
