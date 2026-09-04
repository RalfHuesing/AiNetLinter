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

        var suffixMatches = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => document.FilePath is not null
                && NormalizeVirtualPath(document.FilePath).EndsWith("/" + NormalizeVirtualPath(filePath), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (suffixMatches.Count > 0) return suffixMatches;

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

    internal static string? GetSolutionDirectory(Solution solution)
    {
        if (!string.IsNullOrWhiteSpace(solution.FilePath)
            && Path.IsPathFullyQualified(solution.FilePath))
        {
            return Path.GetDirectoryName(solution.FilePath);
        }

        var projectWithDir = solution.Projects.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.FilePath) && Path.IsPathFullyQualified(p.FilePath));
        if (projectWithDir is not null)
        {
            return Path.GetDirectoryName(projectWithDir.FilePath);
        }

        var docPaths = solution.Projects
            .SelectMany(p => p.Documents)
            .Select(d => d.FilePath)
            .Where(f => !string.IsNullOrWhiteSpace(f) && Path.IsPathFullyQualified(f))
            .ToList();

        if (docPaths.Count > 0)
        {
            return FindCommonDirectory(docPaths);
        }

        return null;
    }

    internal static string? FindCommonDirectory(IReadOnlyList<string?> paths)
    {
        var validPaths = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(p!))
            .ToList();
        if (validPaths.Count == 0) return null;

        var common = Path.GetDirectoryName(validPaths[0]);
        if (string.IsNullOrEmpty(common)) return null;

        for (var i = 1; i < validPaths.Count; i++)
        {
            while (!string.IsNullOrEmpty(common)
                && !validPaths[i].StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !validPaths[i].StartsWith(common + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(validPaths[i], common, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(common);
                if (string.Equals(parent, common, StringComparison.OrdinalIgnoreCase)) break;
                common = parent;
            }
        }

        return string.IsNullOrEmpty(common) ? null : common;
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
