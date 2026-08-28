#nullable enable

using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies;
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
        using var composition = AssemblyAnalysisHostComposition.Create();

        await DaemonHostCommand.RunMcpSessionAsync(connection, registry, composition);

        Assert.False(connection.CancellationToken.IsCancellationRequested);
    }
}
