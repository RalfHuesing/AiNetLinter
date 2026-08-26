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
}
