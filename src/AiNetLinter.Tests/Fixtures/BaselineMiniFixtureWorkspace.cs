#nullable enable

using System.IO;

namespace AiNetLinter.Tests.Fixtures;

public sealed class BaselineMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public BaselineMiniFixtureWorkspace() 
        : base("BaselineMini", "ainetlinter-baseline-mini-")
    {
    }

    public string ConfigPath => Path.Combine(RootPath, "rules.json");

    public string ViolatingClassPath => Path.Combine(RootPath, "src", "BaselineMini", "ViolatingClass.cs");
}
