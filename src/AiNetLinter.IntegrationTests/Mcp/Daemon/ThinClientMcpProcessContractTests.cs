#nullable enable

using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class ThinClientMcpProcessContractTests
{
    public ThinClientMcpProcessContractTests(DaemonEndpointJanitorFixture janitor) => _ = janitor;

    [Fact]
    public async Task NoDaemonEscape_UsesDirectInProcStdioPath()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"EscapeContract\",\"version\":\"1\"}}}",
        };

        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.RootPath,
            frames,
            new McpRawWireRunOptions { NoDaemon = true });

        Assert.NotEmpty(lines);
        Assert.Contains(lines, line => line.Contains("\"jsonrpc\":\"2.0\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NormalMcpServerPath_ConnectsThroughDaemon_AndReportsRuntimeHealth()
    {
        // Der Thin-Client bindet denselben pro Testprozess isolierten Pipe-Endpunkt wie die
        // Daemon-Contracts; deshalb ueber dasselbe Endpunkt-Gate laufen (Janitor + Skip).
        // Budget deckt die legitime Wartezeit auf den eigenen Turn ab.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(240));
        using var endpointLease = await DaemonProcessContractHarness
            .AcquireEndpointAsync(cancellation.Token)
            .ConfigureAwait(false);
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"ThinClientContract\",\"version\":\"1\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"get_server_health\",\"arguments\":{}}}",
        };

        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            fixture.RootPath,
            frames,
            new McpRawWireRunOptions
            {
                NoDaemon = false,
                DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
            });
        var response = McpRawWireTestHarness.FindResponse(lines, 2);
        var result = response.GetProperty("result");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Contains("- Mode: daemon", text, StringComparison.Ordinal);
        Assert.Contains("- connectionId:", text, StringComparison.Ordinal);
        Assert.True(result.TryGetProperty("structuredContent", out var structured), result.ToString());
        Assert.True(structured.TryGetProperty("daemon", out var daemon), structured.ToString());
        Assert.Equal("daemon", daemon.GetProperty("mode").GetString());
        Assert.True(daemon.GetProperty("connectionId").GetInt32() > 0);
        Assert.True(daemon.GetProperty("processId").GetInt32() > 0);
        Assert.True(daemon.GetProperty("uptimeSeconds").GetDouble() >= 0);
        Assert.NotNull(daemon.GetProperty("keys"));
        Assert.False(string.IsNullOrWhiteSpace(daemon.GetProperty("daemonVersion").GetString()));
    }
}
