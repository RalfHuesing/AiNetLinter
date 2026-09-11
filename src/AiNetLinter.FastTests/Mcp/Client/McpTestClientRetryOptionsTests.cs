#nullable enable

using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpTestClientRetryOptionsTests
{
    [Fact]
    public void McpTestClientRetryOptions_DefaultValues_AreSane()
    {
        var options = new McpTestClientRetryOptions();

        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(500, options.BaseDelayMs);
        Assert.Equal(2.0, options.BackoffFactor);
    }

    [Fact]
    public void McpTestClientRetryOptions_OverrideAllProperties()
    {
        var options = new McpTestClientRetryOptions(MaxRetries: 7, BaseDelayMs: 100, BackoffFactor: 1.5);

        Assert.Equal(7, options.MaxRetries);
        Assert.Equal(100, options.BaseDelayMs);
        Assert.Equal(1.5, options.BackoffFactor);
    }
}
