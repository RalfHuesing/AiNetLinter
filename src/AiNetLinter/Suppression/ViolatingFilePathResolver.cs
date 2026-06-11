using AiNetLinter.Models;

namespace AiNetLinter.Suppression;

/// <summary>
/// Löst relative Verstoß-Pfade in absolute Dateipfade auf.
/// </summary>
public static class ViolatingFilePathResolver
{
    /// <summary>
    /// Ermittelt eindeutige absolute Pfade aller Dateien mit Audit-Verstößen.
    /// </summary>
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
