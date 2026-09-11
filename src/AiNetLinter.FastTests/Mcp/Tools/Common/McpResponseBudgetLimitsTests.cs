#nullable enable

using AiNetLinter.Mcp.Tools.Common;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Common;

[Trait("Category", "Unit")]
public sealed class McpResponseBudgetLimitsTests
{
    [Fact]
    public void PublicBudget_UsesDocumentedDefaultAndInclusiveBounds()
    {
        Assert.Equal(16 * 1024, McpResponseBudgetLimits.DefaultBytes);
        Assert.False(McpResponseBudgetLimits.IsPublicBudget(0));
        Assert.False(McpResponseBudgetLimits.IsPublicBudget(511));
        Assert.True(McpResponseBudgetLimits.IsPublicBudget(512));
        Assert.True(McpResponseBudgetLimits.IsPublicBudget(65_536));
        Assert.False(McpResponseBudgetLimits.IsPublicBudget(65_537));
    }
}
