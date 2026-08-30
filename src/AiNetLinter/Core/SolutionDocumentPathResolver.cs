#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Loest Dokumentpfade fuer physische und generierte Roslyn-Dokumente auf.
/// </summary>
internal static class SolutionDocumentPathResolver
{
    internal static Document? Find(Solution solution, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        foreach (var project in solution.Projects)
        {
            var document = project.Documents.FirstOrDefault(document => PathsEquivalent(solution, document.FilePath, filePath));
            if (document is not null) return document;
        }

        return IsBareFileName(filePath) ? FindUniqueByName(solution, filePath) : null;
    }

    private static Document? FindUniqueByName(Solution solution, string fileName)
    {
        var matches = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => string.Equals(document.Name, fileName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool PathsEquivalent(Solution solution, string? left, string right)
    {
        var leftPaths = GetPathVariants(solution, left).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetPathVariants(solution, right).Any(leftPaths.Contains);
    }

    private static IEnumerable<string> GetPathVariants(Solution solution, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) yield break;

        var solutionDir = Path.GetDirectoryName(solution.FilePath ?? string.Empty) ?? string.Empty;
        foreach (var candidate in new[] { path, Path.Combine(solutionDir, path) })
        {
            if (TryGetFullPath(candidate, out var fullPath)) yield return fullPath;
        }
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
