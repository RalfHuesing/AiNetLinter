using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Aggregiert Regelverstöße für die LLM-Triage-Summary nach Datei und Regel.
/// </summary>
public static class ViolationSummaryBuilder
{
    /// <summary>
    /// Gruppiert Verstöße nach relativem Dateipfad, sortiert absteigend nach Anzahl.
    /// </summary>
    public static IReadOnlyList<FileViolationCount> BuildByFile(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot)
    {
        return violations
            .GroupBy(v => PathNormalizer.ToRelative(outputRoot, v.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(g => new FileViolationCount(g.Count(), g.Key))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gruppiert Verstöße nach Regelname, sortiert absteigend nach Anzahl.
    /// </summary>
    public static IReadOnlyList<RuleViolationCount> BuildByRule(
        IReadOnlyCollection<RuleViolation> violations,
        Config? config = null)
    {
        return violations
            .GroupBy(v => v.RuleName, StringComparer.Ordinal)
            .Select(g => new RuleViolationCount(
                g.Count(),
                g.Key,
                config == null ? "general" : RuleMetadataRegistry.Resolve(g.Key, config).Intent))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.RuleName, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
/// Anzahl der Verstöße pro relativer Datei.
/// </summary>
public sealed record FileViolationCount(int Count, string RelativePath);

/// <summary>
/// Anzahl der Verstöße pro Regel.
/// </summary>
public sealed record RuleViolationCount(int Count, string RuleName, string Intent = "general");
