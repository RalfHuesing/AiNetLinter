#nullable enable

using System.Text.Json;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.TestKit;
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

    [Fact]
    public async Task ProjectTargetHealth_UsesDaemonRegistryAndReturnsResidentProject()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        using var isolatedState = TestTempDirectory.Create("thin-client-project-health-");
        string[] warmupFrames =
        [
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"ThinClientProjectHealthContract\",\"version\":\"1\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "get_file_skeleton",
                    arguments = new { filePaths = new[] { "src/SymbolGraphMini/Greeter.cs" } },
                },
            }),
        ];

        string[] healthFrames =
        [
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"ThinClientProjectHealthContract\",\"version\":\"1\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "get_server_health",
                    arguments = new
                    {
                        targetType = "project",
                        targetPath = fixture.RootPath,
                    },
                },
            }),
        ];

        var runOptions = new McpRawWireRunOptions
        {
            NoDaemon = false,
            DaemonIdleExitMinutes = 5,
            LocalAppDataOverride = isolatedState.DirectoryPath,
            DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
        };
        var daemonPid = 0;
        try
        {
            var warmup = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.RootPath,
                warmupFrames,
                runOptions);

            Assert.Equal(0, warmup.ExitCode);
            var warmupResult = McpRawWireTestHarness.FindResponse(warmup.StdoutLines, 2).GetProperty("result");
            Assert.False(
                warmupResult.TryGetProperty("isError", out var warmupIsError) && warmupIsError.GetBoolean(),
                warmupResult.ToString());
            if (warmupResult.TryGetProperty("structuredContent", out var warmupStructured)
                && warmupStructured.TryGetProperty("daemon", out var warmupDaemon))
            {
                daemonPid = warmupDaemon.GetProperty("processId").GetInt32();
            }

            var result = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.RootPath,
                healthFrames,
                runOptions);

            Assert.Equal(0, result.ExitCode);
            var response = McpRawWireTestHarness.FindResponse(result.StdoutLines, 2);
            var responseResult = response.GetProperty("result");
            Assert.False(
                responseResult.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
                response.ToString());

            var structured = responseResult.GetProperty("structuredContent");
            var daemon = structured.GetProperty("daemon");
            daemonPid = daemon.GetProperty("processId").GetInt32();
            Assert.Equal("daemon", daemon.GetProperty("mode").GetString());
            Assert.Contains(
                daemon.GetProperty("keys").EnumerateArray(),
                key => string.Equals(key.GetString(), fixture.RootPath, StringComparison.OrdinalIgnoreCase));

            var project = Assert.Single(structured.GetProperty("projects").EnumerateArray());
            Assert.Equal(fixture.RootPath, project.GetProperty("projectRoot").GetString(), ignoreCase: true);
            Assert.Equal("Loaded", project.GetProperty("loadState").GetString());
            Assert.False(string.IsNullOrWhiteSpace(project.GetProperty("solutionPath").GetString()));
        }
        finally
        {
            TryKillDaemon(daemonPid);
        }
    }

    private static void TryKillDaemon(int processId)
    {
        if (processId <= 0) return;

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            // Der Daemon kann zwischen Antwort und Cleanup bereits beendet sein.
        }
    }
}
