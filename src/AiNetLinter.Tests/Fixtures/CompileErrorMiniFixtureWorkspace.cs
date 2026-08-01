namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des CompileErrorMini-Fixtures fuer EPIC-06-Tests: enthaelt 3 intakte C#-
/// Klassen (ValidClassA/B/C) und 3 Klassen mit absichtlichen Compile-Fehlern (BrokenClassA/B/C).
/// MSBuildWorkspace laedt das Projekt vollstaendig (auch mit Syntax-/Semantik-Fehlern) und meldet
/// die Fehler ueber <c>Compilation.GetDiagnostics()</c> — auf dem die 006-Warnhinweis-Pfade in
/// den 9 MCP-Tools aufsetzen.
/// </summary>
public sealed class CompileErrorMiniFixtureWorkspace : IDisposable
{
    public CompileErrorMiniFixtureWorkspace()
    {
        var sourceRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "CompileErrorMini");
        RootPath = Path.Combine(Path.GetTempPath(), $"ainetlinter-compile-error-mini-{Guid.NewGuid():N}");
        CopyFixture(sourceRoot, RootPath);
    }

    public string RootPath { get; }

    public string PathFor(string fileName) => Path.Combine(RootPath, "src", "CompileErrorMini", fileName);

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
