namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des SymbolGraphMini-Fixtures fuer parallele find_references-Tests.
/// </summary>
public sealed class SymbolGraphMiniFixtureWorkspace : IDisposable
{
    public SymbolGraphMiniFixtureWorkspace()
    {
        var sourceRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "SymbolGraphMini");
        RootPath = Path.Combine(Path.GetTempPath(), $"ainetlinter-symbolgraph-mini-{Guid.NewGuid():N}");
        CopyFixture(sourceRoot, RootPath);
    }

    public string RootPath { get; }

    public string GreeterPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Greeter.cs");

    public string CallerPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Caller.cs");

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
