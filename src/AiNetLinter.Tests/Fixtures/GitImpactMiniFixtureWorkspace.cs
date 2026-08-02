using System.Diagnostics;
using System.Text;

namespace AiNetLinter.Tests.Fixtures;

public sealed class GitImpactMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public GitImpactMiniFixtureWorkspace()
        : base("GitImpactMini", "ainetlinter-gitimpact-mini")
    {
        InitializeGitRepoWithInitialCommit();
    }

    public string CalculatorPath => Path.Combine(RootPath, "src", "GitImpactMini", "Calculator.cs");

    public void ChangeCalculatorAddBodyWithoutCommitting()
    {
        var content = File.ReadAllText(CalculatorPath);
        var changed = content.Replace(
            "public int Add(int a, int b) => a + b;",
            "public int Add(int a, int b) => a + b + 0;");
        File.WriteAllText(CalculatorPath, changed);
    }

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
