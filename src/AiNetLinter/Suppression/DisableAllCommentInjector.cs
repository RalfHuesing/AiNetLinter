using AiNetLinter.Baseline;
using AiNetLinter.Output;

namespace AiNetLinter.Suppression;

/// <summary>
/// Fügt dateiweite ainetlinter-disable-all-Kommentare in C#-Quelldateien ein.
/// </summary>
public static class DisableAllCommentInjector
{
    /// <summary>
    /// Ergebnis eines Inject-Laufs.
    /// </summary>
    public sealed record InjectResult(int TotalFiles, int ModifiedFiles, int SkippedFiles);

    /// <summary>
    /// Fügt den Disable-all-Kommentar oben in alle analysierbaren .cs-Dateien unter path ein.
    /// </summary>
    public static async Task<InjectResult> InjectAsync(string path)
    {
        var absolutePaths = await ResolveSourceFilePathsAsync(path);
        return InjectIntoFiles(absolutePaths);
    }

    /// <summary>
    /// Fügt den Disable-all-Kommentar in eine einzelne Datei ein, sofern noch nicht vorhanden.
    /// </summary>
    public static bool TryInjectIntoFile(string absolutePath)
    {
        var content = File.ReadAllText(absolutePath);
        if (SuppressionCommentParser.ContainsDisableAll(content))
        {
            return false;
        }

        File.WriteAllText(absolutePath, PrependDisableAll(content));
        return true;
    }

    internal static string PrependDisableAll(string content)
    {
        if (content.StartsWith('\uFEFF'))
        {
            return "\uFEFF" + SuppressionCommentParser.DisableAllLine + Environment.NewLine + content[1..];
        }

        return SuppressionCommentParser.DisableAllLine + Environment.NewLine + content;
    }

    private static InjectResult InjectIntoFiles(IReadOnlyList<string> absolutePaths)
    {
        int modified = 0;
        int skipped = 0;

        foreach (var absolutePath in absolutePaths)
        {
            if (TryInjectIntoFile(absolutePath))
            {
                modified++;
            }
            else
            {
                skipped++;
            }
        }

        return new InjectResult(absolutePaths.Count, modified, skipped);
    }

    private static async Task<IReadOnlyList<string>> ResolveSourceFilePathsAsync(string path)
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
