using AiNetLinter.Models;
using AiNetLinter.Output;

namespace AiNetLinter.Baseline;

public static class BaselineViolationFilter
{
    public static IReadOnlyCollection<RuleViolation> Filter(
        IReadOnlyCollection<RuleViolation> violations,
        IReadOnlySet<string> changedFiles,
        string outputRoot)
    {
        if (changedFiles.Count == 0)
        {
            return [];
        }

        return violations
            .Where(v => IsChangedFile(v.FilePath, changedFiles, outputRoot))
            .ToArray();
    }

    private static bool IsChangedFile(string filePath, IReadOnlySet<string> changedFiles, string outputRoot)
    {
        var relative = PathNormalizer.ToRelative(outputRoot, filePath);
        return changedFiles.Contains(relative);
    }
}
