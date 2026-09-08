#nullable enable

using System.IO;

namespace AiNetLinter.TestKit;

/// <summary>
/// Erstellt minimale, MCP-kompatible Solution-Ziele fuer Tests.
/// </summary>
public static class McpTestTargetFactory
{
    /// <summary>
    /// Erstellt eine leere <c>.slnx</c> samt leerer benachbarter MCP-Regeldatei.
    /// </summary>
    public static string CreateSolutionTarget(TestTempDirectory tempDir, string name)
    {
        tempDir.CreateFile(Path.Combine(name, "app.slnx"), string.Empty);
        tempDir.CreateFile(Path.Combine(name, "ainetlinter-rules.json"), "{}");
        return tempDir.GetPath(Path.Combine(name, "app.slnx"));
    }
}
