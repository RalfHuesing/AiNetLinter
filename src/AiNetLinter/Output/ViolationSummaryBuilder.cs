using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

public static class ViolationSummaryBuilder
{
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

public sealed record FileViolationCount(int Count, string RelativePath);

public sealed record RuleViolationCount(int Count, string RuleName, string Intent = "general");
