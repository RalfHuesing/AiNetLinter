#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AiNetLinter.Suppression;

/// <summary>
/// Scans source files for active suppression comments.
/// </summary>
public static class SuppressionScanner
{
    /// <summary>
    /// Scans a single file for `ainetlinter-disable` comments.
    /// </summary>
    public static IReadOnlyList<SuppressionEntry> ScanFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<SuppressionEntry>();
        }

        var entries = new List<SuppressionEntry>();
        try
        {
            var lines = File.ReadLines(filePath);
            int lineNumber = 0;
            foreach (var lineText in lines)
            {
                lineNumber++;
                var ruleName = ParseRuleName(lineText);
                if (ruleName != null)
                {
                    entries.Add(new SuppressionEntry(filePath, lineNumber, ruleName, lineText.Trim()));
                }
            }
        }
        catch (IOException ignored)
        {
            _ = ignored;
            // Ignore file read errors, just return what we have (or empty)
        }

        return entries;
    }

    /// <summary>
    /// Scans all files under targetPath (C# and Razor files) for suppressions.
    /// </summary>
    public static async Task<IReadOnlyList<SuppressionEntry>> ScanAllAsync(string targetPath)
    {
        var files = new List<string>();

        // 1. Get all C# files using the existing resolver
        try
        {
            var csFiles = await SuppressionSourceFileResolver.ResolveAbsolutePathsAsync(targetPath);
            files.AddRange(csFiles);
        }
        catch (Exception ignored)
        {
            _ = ignored;
            // Fallback or ignore
        }

        // 2. Enumerate razor files in target directory if it exists
        string? searchDir = null;
        if (Directory.Exists(targetPath))
        {
            searchDir = Path.GetFullPath(targetPath);
        }
        else if (File.Exists(targetPath))
        {
            searchDir = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        }

        if (searchDir != null)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(searchDir, "*.razor", SearchOption.AllDirectories))
                {
                    if (!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    {
                        files.Add(file);
                    }
                }
            }
            catch (Exception ignored)
            {
                _ = ignored;
                // Ignore directory enumeration issues
            }
        }

        var allEntries = new List<SuppressionEntry>();
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            allEntries.AddRange(ScanFile(file));
        }

        return allEntries;
    }

    private static string? ParseRuleName(string lineText)
    {
        int index = lineText.IndexOf(SuppressionCommentParser.DisableMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        string suffix = lineText.Substring(index + SuppressionCommentParser.DisableMarker.Length).Trim();
        if (suffix.Length == 0)
        {
            return "all";
        }

        // Split by space, comma, semicolon, or comment closers (*@ for razor, */ for block comments)
        var parts = suffix.Split(new[] { ' ', ',', ';', '*', '@', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "all";
        }

        var firstPart = parts[0].Trim();
        if (firstPart.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return "all";
        }
        return firstPart;
    }
}

/// <summary>
/// Model class representing a single suppression entry.
/// </summary>
public sealed record SuppressionEntry(
    string FilePath,
    int LineNumber,
    string RuleName,
    string RawComment
);
