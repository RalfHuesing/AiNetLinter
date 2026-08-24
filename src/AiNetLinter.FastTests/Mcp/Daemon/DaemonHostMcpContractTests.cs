#nullable enable

using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.FastTests.Mcp.Daemon;

// @covers DaemonHostCommand
[Trait("Category", "Unit")]
public sealed class DaemonHostMcpContractTests
{
    [Fact]
    public async Task RunMcpSessionAsync_UsesTheExistingMcpSessionRunnerOnConnectionEof()
    {
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        await using var connection = new DaemonPipeConnection(new MemoryStream());

        await DaemonHostCommand.RunMcpSessionAsync(connection, registry);

        Assert.False(connection.CancellationToken.IsCancellationRequested);
    }
}
