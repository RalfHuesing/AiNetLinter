using AiNetLinter.Models;
using AiNetLinter.Scope;
using AiNetLinter.Suppression;

namespace AiNetLinter.Output;

/// <summary>
/// Erzeugt einen parsebaren Tech-Debt-Report für den Wellen-Workflow.
/// </summary>
public static class DebtReportBuilder
{
    /// <summary>
    /// Baut den Debt-Report aus Disable-all-Dateien und optionalen Audit-Verstößen.
    /// </summary>
    public static async Task<string> BuildAsync(
        string targetPath,
        IReadOnlyCollection<RuleViolation>? violations = null)
    {
        var outputRoot = OutputRootResolver.Resolve(targetPath);
        var absolutePaths = await SuppressionFileResolver.ResolveAbsolutePathsAsync(targetPath);
        var disableAllByFolder = GroupDisableAllByFolder(absolutePaths, outputRoot);
        var waveReady = BuildWaveReadyCandidates(violations, outputRoot);
        var activeSuppressions = await SuppressionScanner.ScanAllAsync(targetPath);
        var suppressionLines = BuildActiveSuppressions(activeSuppressions, outputRoot);

        var lines = new List<string>
        {
            "# AiNetLinter - debt report",
            "## disable-all by folder",
        };
        lines.AddRange(disableAllByFolder.Select(x => $"{x.Count} {x.Folder}"));
        lines.Add("");
        lines.Add("## wave-ready candidates (no disable-all, has violations)");
        lines.AddRange(waveReady);
        lines.Add("");
        lines.Add("## active suppressions by file");
        if (suppressionLines.Count > 0)
        {
            lines.AddRange(suppressionLines);
        }
        else
        {
            lines.Add("(keine aktiven Suppressions gefunden)");
        }

        return string.Join('\n', lines);
    }

    private static IReadOnlyList<string> BuildActiveSuppressions(
        IReadOnlyList<SuppressionEntry> suppressions,
        string outputRoot)
    {
        if (suppressions.Count == 0)
        {
            return [];
        }

        return suppressions
            .GroupBy(s => s.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var relative = PathNormalizer.ToRelative(outputRoot, g.Key);
                var ruleDetails = g
                    .OrderBy(s => s.LineNumber)
                    .ThenBy(s => s.RuleName, StringComparer.Ordinal)
                    .Select(s => $"{s.RuleName} (Zeile {s.LineNumber})");
                return $"- {relative}: {string.Join(", ", ruleDetails)}";
            })
            .ToArray();
    }

    private static IReadOnlyList<FolderCount> GroupDisableAllByFolder(
        IReadOnlyList<string> absolutePaths,
        string outputRoot)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in absolutePaths)
        {
            if (!DisableAllDetector.FileHasDisableAll(path))
            {
                continue;
            }

            var relative = PathNormalizer.ToRelative(outputRoot, path);
            var folder = GetFolderKey(relative);
            counts.TryGetValue(folder, out var current);
            counts[folder] = current + 1;
        }

        return counts
            .Select(x => new FolderCount(x.Value, x.Key))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Folder, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildWaveReadyCandidates(
        IReadOnlyCollection<RuleViolation>? violations,
        string outputRoot)
    {
        if (violations == null || violations.Count == 0)
        {
            return [];
        }

        return violations
            .Where(v => !DisableAllDetector.FileHasDisableAll(v.FilePath))
            .Select(v => PathNormalizer.ToRelative(outputRoot, v.FilePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"1 {x}")
            .ToArray();
    }

    private static string GetFolderKey(string relativePath)
    {
        var lastSep = relativePath.LastIndexOf('/');
        return lastSep > 0 ? relativePath[..(lastSep + 1)] : relativePath;
    }

    private sealed record FolderCount(int Count, string Folder);
}
