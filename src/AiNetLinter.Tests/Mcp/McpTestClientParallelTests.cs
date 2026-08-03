#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Last-Test fuer
/// alle erfolgreich sein (Retry faengt den 010-Volllauf-Flake ab), oder saemtliche Clients
/// ordnungsgemaess disposed werden. Integration-Test, weil er einen echten Server-Subprozess
/// startet — laeuft im Volllauf, nicht im Unit-Slice.
/// </summary>
public sealed class McpTestClientParallelTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => McpTestClient.ConnectAsync(fixture.RootPath, timeoutSeconds: 30,
                retryOptions: new McpTestClientRetryOptions(MaxRetries: 2)))
            .ToArray();

        var clients = await Task.WhenAll(tasks);

        // Erwartung: alle 16 erfolgreich (Retry faengt den 010-Flake ab).
        Assert.Equal(16, clients.Length);
        foreach (var client in clients)
        {
            await client.DisposeAsync();
        }
    }
}
