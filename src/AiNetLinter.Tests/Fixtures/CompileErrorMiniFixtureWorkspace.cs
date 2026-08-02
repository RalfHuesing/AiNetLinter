#nullable enable
using System.IO;

namespace AiNetLinter.Tests.Fixtures;

public sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public CompileErrorMiniFixtureWorkspace()
        : base("CompileErrorMini", "ainetlinter-compile-error-mini")
    {
    }

    public string PathFor(string fileName) => Path.Combine(RootPath, "src", "CompileErrorMini", fileName);
}
