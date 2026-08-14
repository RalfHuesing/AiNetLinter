#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Last-Test fuer parallele MCP-Server-Starts und Tool-Calls ueber 16 gleichzeitige Tasks.
/// Category "Stress", laeuft nur bei manueller/gezielter Ausfuehrung.
/// </summary>
[Trait("Category", "Stress")]
public sealed class McpTestClientParallelTests
{
    [Fact]
    public async Task ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly()
    {
        var tasks = Enumerable.Range(0, 16)
            .Select(async _ =>
            {
                var workspace = new BaselineMiniFixtureWorkspace();
                var host = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));
                await using (host)
                {
                    var tools = await host.ListToolsAsync();
                    Assert.NotNull(tools);
                    Assert.NotEmpty(tools);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);
    }
}
