#nullable enable

using AiNetLinter.Mcp.Registration;

namespace AiNetLinter.FastTests.Mcp.Registration;

[Trait("Category", "Unit")]
public sealed class McpNavigationProjectionTests
{
    [Fact]
    public void Create_ProjectsExplicitStatusHintCodeAndPath()
    {
        var navigation = McpNavigationProjection.Create(
            new McpNavigationProjectionParameters(
                null,
                "partial",
                "partial",
                "Detaillevel reduzieren.",
                "PARTIAL_RESULT",
                "C:\\fixtures\\probe.dll"));

        Assert.Equal("partial", navigation.Status.Operation);
        Assert.Equal("partial", navigation.Status.Completeness);
        Assert.Equal("PARTIAL_RESULT", navigation.Status.Code);
        Assert.Equal("C:\\fixtures\\probe.dll", navigation.Target.TargetPath);
        Assert.Equal("Detaillevel reduzieren.", navigation.Next!.Action);
    }
}
