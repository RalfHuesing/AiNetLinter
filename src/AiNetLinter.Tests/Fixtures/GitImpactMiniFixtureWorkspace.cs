using System.Diagnostics;
using System.Text;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des GitImpactMini-Fixtures mit einem echten, lokal initialisierten
/// Git-Repository (initialer Commit ueber den Ausgangszustand) — fuer Tests des Git-Ref-Zweigs von
/// <see cref="AiNetLinter.Core.DiffImpactAnalyzer.AnalyzeAsync"/> (siehe
/// <c>tasks/codegraph-mcp/. Wiederverwendbares Muster fuer
/// kuenftige Tests, die ebenfalls den Git-Ref-Zweig brauchen.
/// </summary>
public sealed class GitImpactMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public GitImpactMiniFixtureWorkspace()
        : base("GitImpactMini", "ainetlinter-gitimpact-mini")
    {
        InitializeGitRepoWithInitialCommit();
    }

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

    /// <summary>
    /// Aendert den Body von <c>Calculator.Add</c> und committet die Aenderung sofort — erzeugt einen
    /// zweiten Commit, sodass <c>HEAD~1</c> einen echten, auswertbaren Diff liefert (fuer den
    /// Subprozess-Test mit explizitem <c>gitRef</c>-Parameter, siehe <c>McpServerCommandTests</c>).
    /// </summary>
    public void CommitCalculatorAddBodyChange()
    {
        var content = File.ReadAllText(CalculatorPath);
        var changed = content.Replace(
            "public int Add(int a, int b) => a + b;",
            "public int Add(int a, int b) => a + b + 1;");
        File.WriteAllText(CalculatorPath, changed);

        RunGit("add -A");
        RunGit("commit -m second");
    }

    public override void Dispose()
    {
        ClearReadOnlyAttributes(RootPath);
        base.Dispose();
    }

    /// <summary>
    /// Git markiert Objekte im <c>.git</c>-Ordner unter Windows z. T. als schreibgeschuetzt — ohne
    /// diesen Schritt schlaegt <see cref="Directory.Delete(string, bool)"/> mit
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
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"git-Prozess konnte nicht gestartet werden ('git {arguments}').");
        }

        process.StandardInput.Close();

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.Append(e.Data).Append('\n'); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.Append(e.Data).Append('\n'); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'git {arguments}' schlug fehl (Exit {process.ExitCode}): {stderr}");
        }
    }
}
