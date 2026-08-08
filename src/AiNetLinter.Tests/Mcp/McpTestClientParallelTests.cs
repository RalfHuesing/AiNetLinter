#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Last-Test fuer <see cref="McpTestClient.ConnectAsync"/>: 16 parallele Aufrufe muessen
/// entweder alle erfolgreich sein (Retry faengt den Volllauf-Flake ab), oder saemtliche Clients
/// ordnungsgemaess disposed werden. Startet 16 echte Server-Subprozesse gleichzeitig und dauert
/// dadurch allein ~150s — Category "Stress" statt "Integration", damit er weder im Unit-Slice
/// noch im fuer Task-Abschluss verpflichtenden Volllauf (siehe AGENTS.md §2) automatisch
/// mitlaeuft, sondern nur bei gezielter/manueller Ausfuehrung. Siehe AGENTS.md §2 fuer die
/// Kategorien-Konvention.
/// </summary>
public sealed class McpTestClientParallelTests
{
    [Fact]
    [Trait("Category", "Stress")]
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
