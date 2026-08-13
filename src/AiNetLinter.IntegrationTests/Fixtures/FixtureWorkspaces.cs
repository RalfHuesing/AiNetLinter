#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Fixtures;

internal abstract class FixtureWorkspace : IDisposable
{
    private readonly IsolatedFixtureLease lease;

    protected FixtureWorkspace(string fixtureName) => lease = IsolatedFixtureLease.CopyFixture(FindSolutionRoot(), fixtureName);

    public string RootPath => lease.RootPath;

    public void Dispose() => lease.Dispose();

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

internal sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspace { public CompileErrorMiniFixtureWorkspace() : base("CompileErrorMini") { } }
internal sealed class SingleCompileErrorMiniFixtureWorkspace : FixtureWorkspace { public SingleCompileErrorMiniFixtureWorkspace() : base("SingleCompileErrorMini") { } }
internal sealed class BlazorPartialMiniFixtureWorkspace : FixtureWorkspace
{
    public BlazorPartialMiniFixtureWorkspace() : base("BlazorPartialMini") { }
    public string SiteViewCsPath => Path.Combine(RootPath, "src", "BlazorPartialMini", "SiteView.razor.cs");
}
