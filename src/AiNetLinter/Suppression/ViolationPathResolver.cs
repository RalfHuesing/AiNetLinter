using AiNetLinter.Models;

namespace AiNetLinter.Suppression;

public static class ViolationPathResolver
{
    public static IReadOnlyList<string> ResolveAbsolutePaths(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot)
    {
        var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var violation in violations)
        {
            var absolutePath = ToAbsolutePath(outputRoot, violation.FilePath);
            if (File.Exists(absolutePath))
            {
                resolvedPaths.Add(absolutePath);
            }
        }

        return resolvedPaths.ToArray();
    }

    private static string ToAbsolutePath(string outputRoot, string relativePath)
    {
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(outputRoot, normalizedRelative));
    }
}
