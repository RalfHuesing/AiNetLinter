#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal sealed record FileTreeMatchCriteria(
    string RelativePath,
    string? Extension,
    IReadOnlyList<string> Extensions,
    string? FileFilter,
    IReadOnlyList<string> ExcludePatterns);

internal static class FileTreeFilter
{
    internal static string[] NormalizeExtensions(IReadOnlyList<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0 || extensions.Any(IsAllExtension)) return [];

        return extensions
            .Select(extension => extension.Trim().ToLowerInvariant())
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool Matches(FileTreeMatchCriteria criteria)
    {
        if (criteria.Extensions.Count > 0 && (criteria.Extension is null ||
            !criteria.Extensions.Contains(criteria.Extension, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(criteria.FileFilter) &&
            !MatchesPathOrFileName(criteria.RelativePath, criteria.FileFilter))
        {
            return false;
        }

        return !criteria.ExcludePatterns.Any(pattern =>
            MatchesPathOrFileName(criteria.RelativePath, pattern));
    }

    private static bool MatchesPathOrFileName(string relativePath, string pattern)
    {
        var trimmed = pattern.Trim();
        var matchTarget = (!trimmed.Contains('/') && !trimmed.Contains('\\'))
            ? Path.GetFileName(relativePath)
            : relativePath;

        return PathGlobMatcher.Matches(matchTarget, trimmed);
    }

    internal static bool IsValidExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var normalized = extension.Trim();
        if (normalized == "*") return true;
        if (!normalized.StartsWith(".", StringComparison.Ordinal)) normalized = $".{normalized}";
        return normalized.Length > 1
            && normalized.IndexOfAny(['/', '\\', ':', '*', '?', '\0']) < 0;
    }

    internal static bool IsValidRelativeGlob(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        var normalized = PathNormalizer.NormalizeSeparators(pattern.Trim());
        if (normalized.IndexOf('\0') >= 0 || Path.IsPathRooted(normalized)) return false;

        return normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not ".." and not ".");
    }

    private static bool IsAllExtension(string extension) => extension.Trim() == "*";
}
