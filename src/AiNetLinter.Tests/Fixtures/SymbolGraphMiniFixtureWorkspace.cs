#nullable enable

using System.IO;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des SymbolGraphMini-Fixtures fuer parallele find_references-Tests.
/// </summary>
public sealed class SymbolGraphMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public SymbolGraphMiniFixtureWorkspace() 
        : base("SymbolGraphMini", "ainetlinter-symbolgraph-mini-")
    {
    }

    public string GreeterPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Greeter.cs");

    public string CallerPath => Path.Combine(RootPath, "src", "SymbolGraphMini", "Caller.cs");
}
