#nullable enable

using System;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class ResponseBudgetRecoveryTests
{
    private const int VisibleBytes = 2_048;
    private const int RequestedBytes = 1_024;

    [Fact]
    public void FinalBudgetHandlers_ReturnExactVisibleMinimumAndExecutableRetry()
    {
        var source = McpToolResults.Text(new string('x', VisibleBytes));

        AssertRecovery(FindReferencesTool.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(GetTypeHierarchyTool.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(FindImplementationsTool.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(FindSymbolTool.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(GetSymbolBodyTool.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(GetClassStructureResponseBudget.ApplyFinalResponseBudget(source, RequestedBytes));
        AssertRecovery(CallGraphResponseBudget.ApplyFinalResponseBudget(source, RequestedBytes));
    }

    private static void AssertRecovery(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, StringComparison.Ordinal);
        Assert.Contains($"minimumResponseBytes: {VisibleBytes}", text, StringComparison.Ordinal);
        Assert.Contains(
            $"retry: denselben Aufruf mit maxResponseBytes={VisibleBytes} wiederholen.",
            text,
            StringComparison.Ordinal);
    }
}
