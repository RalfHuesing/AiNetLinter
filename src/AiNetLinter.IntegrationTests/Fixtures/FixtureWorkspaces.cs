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

    protected FixtureWorkspace(string fixtureName) => lease = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), fixtureName);

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

    public void ChangeCalculatorNormalizeBodyWithoutCommitting()
    {
        var content = File.ReadAllText(CalculatorPath);
        File.WriteAllText(CalculatorPath, content.Replace(
            "private int Normalize(int value) => value * 2;", "private int Normalize(int value) => value * 3;"));
    }

    public void CommitCalculatorAddBodyChange()
    {
        ChangeCalculatorAddBodyWithoutCommitting();
        FixtureGit.Run(RootPath, "add -A");
        FixtureGit.Run(RootPath, "commit -m change");
    }

    protected override void PrepareForDelete() => FixtureFileAttributes.NormalizeTree(RootPath);

    private void InitializeGitRepository()
    {
        FixtureGit.Run(RootPath, "init");
        FixtureGit.Run(RootPath, "config user.email ainetlinter-test@example.com");
        FixtureGit.Run(RootPath, "config user.name AiNetLinterTest");
        FixtureGit.Run(RootPath, "add -A");
        FixtureGit.Run(RootPath, "commit -m initial");
    }
}

// Setzt Datei- und Ordnerattribute auf Normal, damit read-only Git-Objekte beim Teardown loeschbar sind.
internal static class FixtureFileAttributes
{
    internal static void NormalizeTree(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return;

        foreach (var path in Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        File.SetAttributes(rootPath, FileAttributes.Normal);
    }
}

// Gemeinsamer synchroner git-Aufrufer der Fixture-Workspaces.
internal static class FixtureGit
{
    // ainetlinter-disable BanBlockingTaskAccess
    internal static void Run(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
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

/// <summary>
/// Mini-Git-Workspace fuer das Change-Context-Szenario: legt die Produktionsquelldateien
/// der <see cref="ChangeContextScenarioFactory"/> physisch ab, committet sie initial und
/// erlaubt uncommittete Body-Aenderungen beider Methoden. Das naechstliegende .git ist das
/// des Workspaces — ein Analyzer-Diff-Lauf sieht exakt die Szenario-Aenderungen.
/// </summary>
internal sealed class ChangeContextMiniWorkspace : IDisposable
{
    private readonly TestTempDirectory tempDirectory;

    public ChangeContextMiniWorkspace()
    {
        tempDirectory = TestTempDirectory.Create("change-context-scenario-");
        try
        {
            RootPath = tempDirectory.DirectoryPath;
            WriteScenarioFiles();
            InitializeGitRepository();
        }
        catch
        {
            tempDirectory.Dispose();
            throw;
        }
    }

    public string RootPath { get; }

    public RoslynTestSolution CreateSolution() => ChangeContextScenarioFactory.CreateSolution(RootPath);

    /// <summary>Aendert beide Methoden-Body-Zeilen uncommittet — je eine Hunk-Zeile pro Datei.</summary>
    public void ChangeBothMethodBodiesWithoutCommitting()
    {
        foreach (var (projectName, fileName, content) in ChangeContextScenarioFactory.GetChangedProductionSources())
        {
            File.WriteAllText(Path.Combine(RootPath, projectName, fileName), content);
        }
    }

    public void Dispose()
    {
        FixtureFileAttributes.NormalizeTree(RootPath);
        tempDirectory.Dispose();
    }

    private void WriteScenarioFiles()
    {
        foreach (var (projectName, fileName, content) in ChangeContextScenarioFactory.GetProductionSources())
        {
            var path = Path.Combine(RootPath, projectName, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    private void InitializeGitRepository()
    {
        FixtureGit.Run(RootPath, "init");
        FixtureGit.Run(RootPath, "config user.email change-context-scenario@example.com");
        FixtureGit.Run(RootPath, "config user.name ChangeContextScenario");
        FixtureGit.Run(RootPath, "add -A");
        FixtureGit.Run(RootPath, "commit -m initial");
    }
}

internal sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspace { public CompileErrorMiniFixtureWorkspace() : base("CompileErrorMini") { } }
internal sealed class SingleCompileErrorMiniFixtureWorkspace : FixtureWorkspace { public SingleCompileErrorMiniFixtureWorkspace() : base("SingleCompileErrorMini") { } }
internal sealed class BlazorPartialMiniFixtureWorkspace : FixtureWorkspace
{
    public BlazorPartialMiniFixtureWorkspace() : base("BlazorPartialMini") { }
    public string SiteViewCsPath => Path.Combine(RootPath, "src", "BlazorPartialMini", "SiteView.razor.cs");
}
