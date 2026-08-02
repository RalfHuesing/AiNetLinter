using AiNetLinter.Models;
using AiNetLinter.Output;
using AiNetLinter.Suppression;

namespace AiNetLinter.Scope;

public static class ViolationScopeFilter
{
    public static IReadOnlyCollection<RuleViolation> Apply(
        IReadOnlyCollection<RuleViolation> violations,
        ViolationScopeOptions options,
        string outputRoot)
    {
        IEnumerable<RuleViolation> result = violations;

        if (options.WaveReady)
        {
            result = result.Where(v => !IsDisableAllFile(v.FilePath));
        }

        if (options.GitChangedFiles.Count > 0)
        {
            var changed = new HashSet<string>(options.GitChangedFiles, StringComparer.OrdinalIgnoreCase);
            result = result.Where(v => changed.Contains(NormalizeForCompare(v.FilePath, outputRoot)));
        }

        if (options.OnlyChangedFiles.Count > 0)
        {
            var changed = new HashSet<string>(options.OnlyChangedFiles, StringComparer.OrdinalIgnoreCase);
            result = result.Where(v => changed.Contains(NormalizeForCompare(v.FilePath, outputRoot)));
        }

        return result.ToArray();
    }

    private static bool IsDisableAllFile(string filePath) =>
        DisableAllDetector.FileHasDisableAll(filePath);

    private static string NormalizeForCompare(string filePath, string outputRoot) =>
        PathNormalizer.ToRelative(outputRoot, filePath).Replace('\\', '/');
}

public sealed record ViolationScopeOptions
{
    public bool WaveReady { get; init; }
    public IReadOnlyCollection<string> GitChangedFiles { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> OnlyChangedFiles { get; init; } = Array.Empty<string>();
}
