using System.Diagnostics;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des GitImpactMini-Fixtures mit einem echten, lokal initialisierten
/// Git-Repository (initialer Commit ueber den Ausgangszustand) — fuer Tests des Git-Ref-Zweigs von
/// <see cref="AiNetLinter.Core.DiffImpactAnalyzer.AnalyzeAsync"/> (siehe
/// <c>tasks/codegraph-mcp/step-005/step-plan.md</c>, Datei 6/7). Wiederverwendbares Muster fuer
/// kuenftige Tests, die ebenfalls den Git-Ref-Zweig brauchen.
/// </summary>
public sealed class GitImpactMiniFixtureWorkspace : IDisposable
{
    public GitImpactMiniFixtureWorkspace()
    {
        var sourceRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", "GitImpactMini");
        RootPath = Path.Combine(Path.GetTempPath(), $"ainetlinter-gitimpact-mini-{Guid.NewGuid():N}");
        CopyFixture(sourceRoot, RootPath);
        InitializeGitRepoWithInitialCommit();
    }

    public string RootPath { get; }

    public string CalculatorPath => Path.Combine(RootPath, "src", "GitImpactMini", "Calculator.cs");

    /// <summary>
    /// Aendert die Signatur/den Body von <c>Calculator.Add</c> ohne zu committen — bildet den Fall
    /// "uncommittete Aenderungen" ab, den <c>get_impact</c> ohne <c>gitRef</c>-Parameter abdeckt.
    /// </summary>
    public void ChangeCalculatorAddBodyWithoutCommitting()
    {
        var content = File.ReadAllText(CalculatorPath);
        var changed = content.Replace(
            "public int Add(int a, int b) => a + b;",
            "public int Add(int a, int b) => a + b + 0;");
        File.WriteAllText(CalculatorPath, changed);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            ClearReadOnlyAttributes(RootPath);
            Directory.Delete(RootPath, recursive: true);
        }
    }

    /// <summary>
    /// Git markiert Objekte im <c>.git</c>-Ordner unter Windows z. T. als schreibgeschuetzt —
    /// ohne diesen Schritt schlaegt <see cref="Directory.Delete(string, bool)"/> mit
    /// <see cref="UnauthorizedAccessException"/> fehl.
    /// </summary>
    private static void ClearReadOnlyAttributes(string rootPath)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        File.SetAttributes(rootPath, FileAttributes.Normal);
    }

    private void InitializeGitRepoWithInitialCommit()
    {
        RunGit("init");
        RunGit("config user.email ainetlinter-test@example.com");
        RunGit("config user.name AiNetLinterTest");
        RunGit("add -A");
        RunGit("commit -m initial");
    }

    private void RunGit(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = RootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"git-Prozess konnte nicht gestartet werden ('git {arguments}').");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"'git {arguments}' schlug fehl (Exit {process.ExitCode}): {stderr}");
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
