#nullable enable

using AiNetLinter.Mcp.Lifetime;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class ParentProcessDetectorTests
{
    [Fact]
    public void TryGetParentProcessId_CurrentProcess_ReturnsPositivePid()
    {
        var parentProcessId = ParentProcessDetector.TryGetParentProcessId();

        Assert.True(parentProcessId is > 0);
        Assert.NotEqual(Environment.ProcessId, parentProcessId);
    }

    [Fact]
    public void TryParseProcStatParentProcessId_HandlesCommandWithClosingParenthesis()
    {
        const string stat = "42 (agent) host) S 1234 42 42 0 0 0";

        var parentProcessId = ParentProcessDetector.TryParseProcStatParentProcessId(stat);

        Assert.Equal(1234, parentProcessId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("42 (agent)")]
    [InlineData("42 (agent) S not-a-pid")]
    public void TryParseProcStatParentProcessId_InvalidContent_ReturnsNull(string stat)
    {
        Assert.Null(ParentProcessDetector.TryParseProcStatParentProcessId(stat));
    }
}
