#nullable enable

using System.IO;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des BaselineMini-Fixtures fuer parallele CLI-Tests.
/// </summary>
public sealed class BaselineMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public BaselineMiniFixtureWorkspace() 
        : base("BaselineMini", "ainetlinter-baseline-mini-")
    {
    }

    public string ConfigPath => Path.Combine(RootPath, "rules.json");

    public string ViolatingClassPath => Path.Combine(RootPath, "src", "BaselineMini", "ViolatingClass.cs");
}
