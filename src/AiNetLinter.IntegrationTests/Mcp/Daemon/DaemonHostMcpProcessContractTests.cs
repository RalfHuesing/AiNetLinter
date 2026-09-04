#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Daemon;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class DaemonHostMcpProcessContractTests
{
    public DaemonHostMcpProcessContractTests(DaemonEndpointJanitorFixture janitor) => _ = janitor;

    [Fact]
    public async Task HostPipeHandshakeThenMcpInitializeListsToolsAndExitsIdle()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        using var temp = TestTempDirectory.Create("daemon-mcp-process-contract-");
        // Exklusives Endpunkt-Gate mit bis zu vier Daemon-Contracts: Wartezeit auf den
        // eigenen Turn ist legitim und muss das Budget ueberleben; die eigentlichen
        // Testphasen haben eigene Timeouts (Readiness-Retry, WaitForExitAsync).
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(240));
        using var endpointLease = await DaemonProcessContractHarness
            .AcquireEndpointAsync(cancellation.Token)
            .ConfigureAwait(false);
        var spec = new DaemonProcessSpec(fixture.RootPath, temp.DirectoryPath, 0.1m);

        await using var daemon = await DaemonProcessContractHarness
            .StartAsync(spec, cancellation.Token)
            .ConfigureAwait(false);
        await using (var connection = await DaemonProcessContractHarness
            .ConnectWhenReadyAsync(cancellation.Token)
            .ConfigureAwait(false))
        {
            var welcome = await DaemonProcessContractHarness
                .PerformHandshakeAsync(connection, spec, cancellation.Token)
                .ConfigureAwait(false);
            Assert.Equal(DaemonProtocol.Welcome, welcome.Type);
            Assert.Equal(DaemonProtocol.Version, welcome.ProtocolVersion);

            var clientTransport = new StreamClientTransport(connection.Stream, connection.Stream);
            await using var client = await McpClient
                .CreateAsync(clientTransport, cancellationToken: cancellation.Token)
                .ConfigureAwait(false);
            var tools = await client.ListToolsAsync(cancellationToken: cancellation.Token).ConfigureAwait(false);

            Assert.Contains(tools, tool => tool.Name == "find_symbol");
            Assert.Contains(tools, tool => tool.Name == "get_violations");

            var inspect = await client.CallToolAsync(
                "inspect_assembly",
                new Dictionary<string, object?>
                {
                    ["targetType"] = "assembly",
                    ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                    ["typeName"] = nameof(McpCodeGraphServer),
                    ["exactTypeName"] = true,
                    ["maxMembers"] = 10
                },
                cancellationToken: cancellation.Token);
            Assert.NotEqual(true, inspect.IsError);
            Assert.Contains(
                "Quelle: Dekompilat",
                Assert.IsType<TextContentBlock>(Assert.Single(inspect.Content)).Text,
                StringComparison.Ordinal);

            var extensions = await client.CallToolAsync(
                "find_assembly_extensions",
                new Dictionary<string, object?>
                {
                    ["targetType"] = "assembly",
                    ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                    ["maxResults"] = 10
                },
                cancellationToken: cancellation.Token);
            Assert.NotEqual(true, extensions.IsError);
            Assert.Contains(
                "Assembly-Extensions:",
                Assert.IsType<TextContentBlock>(Assert.Single(extensions.Content)).Text,
                StringComparison.Ordinal);

            var context = await client.CallToolAsync(
                "get_assembly_context",
                new Dictionary<string, object?>
                {
                    ["targetType"] = "assembly",
                    ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                    ["maxResults"] = 1,
                    ["includeMetrics"] = false,
                },
                cancellationToken: cancellation.Token);
            Assert.NotEqual(true, context.IsError);
            Assert.True(context.StructuredContent.HasValue);
            Assert.Equal("assembly", context.StructuredContent!.Value.GetProperty("targetType").GetString());
            Assert.True(context.StructuredContent.Value.TryGetProperty("analysis", out _));
        }

        var result = await daemon.WaitForExitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        Assert.False(result.TimedOut, result.Error);
        Assert.True(result.ExitCode == 0, result.Error);
    }
}
