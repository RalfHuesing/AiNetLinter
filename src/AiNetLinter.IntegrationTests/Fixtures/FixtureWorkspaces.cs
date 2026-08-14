#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Fixtures;

internal abstract class FixtureWorkspace : IDisposable
{
    private readonly IsolatedFixtureLease lease;
    private int disposed;

    protected FixtureWorkspace(string fixtureName) => lease = IsolatedFixtureLease.CopyFixture(FindSolutionRoot(), fixtureName);

    public string RootPath => lease.RootPath;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try
        {
            PrepareForDelete();
        }
        finally
        {
            lease.Dispose();
        }
    }

    protected virtual void PrepareForDelete() { }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiNetLinter.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}

internal sealed class BaselineMiniFixtureWorkspace : FixtureWorkspace
{
    public BaselineMiniFixtureWorkspace() : base("BaselineMini") { }
    public string ConfigPath => Path.Combine(RootPath, "rules.json");
    public string ViolatingClassPath => Path.Combine(RootPath, "src", "BaselineMini", "ViolatingClass.cs");
}

internal sealed class SymbolGraphMiniFixtureWorkspace : FixtureWorkspace
{
    public SymbolGraphMiniFixtureWorkspace() : base("SymbolGraphMini") { }
    public string GreeterPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Greeter.cs");
    public string CallerPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Caller.cs");
    public string OtherCallerPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "OtherCaller.cs");
}

internal sealed class GitImpactMiniFixtureWorkspace : FixtureWorkspace
{
    public GitImpactMiniFixtureWorkspace() : base("GitImpactMini") => InitializeGitRepository();

    public string CalculatorPath => Path.Combine(RootPath, "src", "GitImpactMini", "Calculator.cs");

    public void ChangeCalculatorAddBodyWithoutCommitting()
    {
        var content = File.ReadAllText(CalculatorPath);
        File.WriteAllText(CalculatorPath, content.Replace(
            "public int Add(int a, int b) => a + b;", "public int Add(int a, int b) => a + b + 0;"));
    }

    public void CommitCalculatorAddBodyChange()
    {
        ChangeCalculatorAddBodyWithoutCommitting();
        RunGit("add -A");
        RunGit("commit -m change");
    }

    protected override void PrepareForDelete()
    {
        if (!Directory.Exists(RootPath)) return;
        foreach (var path in Directory.EnumerateFileSystemEntries(RootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        File.SetAttributes(RootPath, FileAttributes.Normal);
    }

    private void InitializeGitRepository()
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
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Konnte git nicht starten ('{arguments}').");
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0) throw new InvalidOperationException($"git {arguments} schlug fehl: {stderrTask.Result}");
    }
}

internal sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspace { public CompileErrorMiniFixtureWorkspace() : base("CompileErrorMini") { } }
internal sealed class SingleCompileErrorMiniFixtureWorkspace : FixtureWorkspace { public SingleCompileErrorMiniFixtureWorkspace() : base("SingleCompileErrorMini") { } }
internal sealed class BlazorPartialMiniFixtureWorkspace : FixtureWorkspace
{
    public BlazorPartialMiniFixtureWorkspace() : base("BlazorPartialMini") { }
    public string SiteViewCsPath => Path.Combine(RootPath, "src", "BlazorPartialMini", "SiteView.razor.cs");
}
