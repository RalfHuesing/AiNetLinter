#nullable enable

using Xunit;
using AiNetLinter.Mcp;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpRegistrationInstructionsTests
{
    [Theory]
    [InlineData(@"C:\Tools\AiNetLinter.exe", @"C:\Tools\AiNetLinter.dll", true, @"C:\Tools\AiNetLinter.exe", 1)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", @"C:\Tools\AiNetLinter.dll", true, @"C:\Program Files\dotnet\dotnet.exe", 2)]
    [InlineData(null, @"C:\Tools\AiNetLinter.dll", false, "ainetlinter", 1)]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", null, false, "ainetlinter", 1)]
    public void ResolveLaunch_ProducesUsableHostCommand(
        string? processPath,
        string? assemblyPath,
        bool expectedRuntimeResolution,
        string expectedCommand,
        int expectedArgumentCount)
    {
        var launch = McpRegistrationInstructions.ResolveLaunch(processPath, assemblyPath);

        Assert.Equal(expectedRuntimeResolution, launch.IsRuntimeResolved);
        Assert.Equal(expectedCommand, launch.Command);
        Assert.Equal(expectedArgumentCount, launch.Arguments.Count);
        Assert.Equal("--mcp-server", launch.Arguments[^1]);
    }

    [Fact]
    public void BuildRuntimeBlock_EmitsJsonRegistrationWithRuntimePath()
    {
        var block = McpRegistrationInstructions.BuildRuntimeBlock(
            @"C:\Program Files\AiNetLinter\AiNetLinter.exe",
            assemblyPath: null);

        Assert.Contains("## Laufzeitpfad des MCP-Servers", block);
        Assert.Contains("AiNetLinter.exe", block);
        Assert.Contains("\"args\": [\"--mcp-server\"]", block);
    }
}
