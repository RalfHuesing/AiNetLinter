namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des BaselineMini-Fixtures fuer parallele CLI-Tests.
/// </summary>
public sealed class BaselineMiniFixtureWorkspace : IDisposable
{
    public BaselineMiniFixtureWorkspace()
    {
        var sourceRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "BaselineMini");
        RootPath = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-mini-{Guid.NewGuid():N}");
        CopyFixture(sourceRoot, RootPath);
    }

    public string RootPath { get; }

    public string ConfigPath => Path.Combine(RootPath, "rules.json");

    public string ViolatingClassPath => Path.Combine(RootPath, "src", "BaselineMini", "ViolatingClass.cs");

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static void CopyFixture(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            if (IsGeneratedPath(relativePath))
            {
                continue;
            }

            var targetFile = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    private static bool IsGeneratedPath(string relativePath)
    {
        var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains("obj", StringComparer.OrdinalIgnoreCase) ||
               parts.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }
}
