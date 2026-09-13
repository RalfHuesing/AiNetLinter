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

    [Fact]
    public void Create_FromErrorCallToolResult_ExtractsErrorStatusAndAction()
    {
        var result = new ModelContextProtocol.Protocol.CallToolResult
        {
            IsError = false,
            Content = [new ModelContextProtocol.Protocol.TextContentBlock
            {
                Text = "[ERROR]: INVALID_ARGUMENT: message\n  hint: bitte prüfen",
            }],
        };

        var navigation = McpNavigationProjection.Create(result, null, "C:\\test.sln");

        Assert.Equal("error", navigation.Status.Operation);
        Assert.Equal("not_applicable", navigation.Status.Completeness);
        Assert.Equal("bitte prüfen", navigation.Next!.Action);
    }

    [Fact]
    public void Create_FromErrorWithRetryAndHint_PrioritizesRetry()
    {
        var result = new ModelContextProtocol.Protocol.CallToolResult
        {
            IsError = false,
            Content = [new ModelContextProtocol.Protocol.TextContentBlock
            {
                Text = "[ERROR]: RESPONSE_BUDGET_TOO_SMALL: message\n  hint: budget erhöhen\n  retry: denselben Aufruf wiederholen",
            }],
        };

        var navigation = McpNavigationProjection.Create(result, null, "C:\\test.sln");

        Assert.Equal("error", navigation.Status.Operation);
        Assert.Equal("denselben Aufruf wiederholen", navigation.Next!.Action);
    }
}
