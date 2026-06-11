using System.Diagnostics;
using AiNetLinter.Output;

namespace AiNetLinter.Scope;

/// <summary>
/// Ermittelt geänderte Dateien via git diff (optional, nur wenn .git vorhanden).
/// </summary>
public static class GitChangedFilesResolver
{
    /// <summary>
    /// Liefert relative Pfade geänderter .cs-Dateien seit dem angegebenen Git-Ref.
    /// </summary>
    public static IReadOnlyCollection<string> ResolveSince(string targetPath, string gitSinceRef)
    {
        var repoRoot = FindGitRoot(targetPath);
        if (repoRoot == null)
        {
            return [];
        }

        var output = RunGitDiff(repoRoot, gitSinceRef);
        return output == null ? [] : ParseCsFiles(output, repoRoot, targetPath);
    }

    private static string? FindGitRoot(string startPath)
    {
        var current = File.Exists(startPath) ? Path.GetDirectoryName(startPath) : startPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static string? RunGitDiff(string repoRoot, string gitSinceRef)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff --name-only {gitSinceRef}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : null;
    }

    private static IReadOnlyCollection<string> ParseCsFiles(string gitOutput, string repoRoot, string targetPath)
    {
        var outputRoot = OutputRootResolver.Resolve(targetPath);
        var result = new List<string>();

        foreach (var line in gitOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            TryAddCsFile(line, repoRoot, outputRoot, result);
        }

        return result;
    }

    private static void TryAddCsFile(string line, string repoRoot, string outputRoot, List<string> result)
    {
        if (!line.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var absolute = Path.GetFullPath(Path.Combine(repoRoot, line.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        result.Add(PathNormalizer.ToRelative(outputRoot, absolute));
    }
}
