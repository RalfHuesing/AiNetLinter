using AiNetLinter.Baseline;
using AiNetLinter.Output;

namespace AiNetLinter.Suppression;

/// <summary>
/// Ermittelt analysierbare C#-Quelldateien unter einem CLI-Pfad.
/// </summary>
public static class SuppressionFileResolver
{
    /// <summary>
    /// Liefert absolute Pfade aller analysierbaren .cs-Dateien unter path.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ResolveAbsolutePathsAsync(string path)
    {
        if (CanLoadSolution(path))
        {
            return await ResolveFromSolutionAsync(path);
        }

        if (Directory.Exists(path))
        {
            return EnumerateCsFilesInDirectory(Path.GetFullPath(path));
        }

        throw new FileNotFoundException($"Pfad nicht gefunden oder keine Solution unter: {path}");
    }

    private static async Task<IReadOnlyList<string>> ResolveFromSolutionAsync(string path)
    {
        using var catalog = await SourceFileCatalog.LoadAsync(path);
        var outputRoot = OutputRootResolver.Resolve(path);
        return catalog.GetSourceFiles(outputRoot)
            .Select(entry => entry.AbsolutePath)
            .ToArray();
    }

    private static bool CanLoadSolution(string path)
    {
        if (IsSolutionFile(path))
        {
            return true;
        }

        return Directory.Exists(path) && FindSolutionInDirectory(path) != null;
    }

    private static string? FindSolutionInDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.slnx")
            .Concat(Directory.GetFiles(directory, "*.sln"))
            .ToArray();
        return files.Length > 0 ? files[0] : null;
    }

    private static bool IsSolutionFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> EnumerateCsFilesInDirectory(string directory)
    {
        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsGeneratedPath(file))
            {
                files.Add(file);
            }
        }

        return files;
    }

    private static bool IsGeneratedPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
               path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
               path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }
}
