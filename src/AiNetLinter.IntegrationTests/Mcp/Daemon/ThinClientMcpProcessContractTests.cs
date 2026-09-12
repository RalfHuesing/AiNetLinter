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
            fixture.SolutionPath,
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
            fixture.SolutionPath,
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
        // Exklusives Endpunkt-Gate verhindert, dass parallele xUnit-Kollektionen
        // denselben Daemon teilen oder dessen PID im Cleanup beenden. Das Budget
        // deckt die legitime Wartezeit auf den eigenen Turn ab.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(240));
        using var endpointLease = await DaemonProcessContractHarness
            .AcquireEndpointAsync(cancellation.Token)
            .ConfigureAwait(false);
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var solutionPath = fixture.SolutionPath;
        using var isolatedState = TestTempDirectory.Create("thin-client-project-health-");
        string[] frames =
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
            JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/call",
                @params = new
                {
                    name = "get_server_health",
                    arguments = new
                    {
                        targetPath = solutionPath,
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
        var daemonSpec = new DaemonProcessSpec(
            fixture.SolutionPath,
            isolatedState.DirectoryPath,
            IdleExitMinutes: 5);
        var daemonPid = 0;
        try
        {
            var result = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.SolutionPath,
                frames,
                runOptions);

            Assert.Equal(0, result.ExitCode);
            var warmupResult = McpRawWireTestHarness.FindResponse(result.StdoutLines, 2).GetProperty("result");
            Assert.False(
                warmupResult.TryGetProperty("isError", out var warmupIsError) && warmupIsError.GetBoolean(),
                warmupResult.ToString());
            daemonPid = await DaemonProcessContractHarness
                .GetDaemonProcessIdAsync(daemonSpec, cancellation.Token)
                .ConfigureAwait(false);

            var response = McpRawWireTestHarness.FindResponse(result.StdoutLines, 3);
            var responseResult = response.GetProperty("result");
            Assert.False(
                responseResult.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
                response.ToString());

            var structured = responseResult.GetProperty("structuredContent");
            Assert.False(structured.TryGetProperty("daemon", out _));
            var project = structured.GetProperty("project");
            Assert.Equal(solutionPath, project.GetProperty("targetPath").GetString(), ignoreCase: true);
            var loadState = project.GetProperty("loadState").GetString();
            Assert.Contains(loadState, new[] { "Loaded", "Loading" });
            if (loadState == "Loaded")
            {
                Assert.False(string.IsNullOrWhiteSpace(project.GetProperty("solutionPath").GetString()));
            }
            var navigation = structured.GetProperty("navigation");
            var snapshot = navigation.GetProperty("snapshot");
            if (loadState == "Loaded")
            {
                Assert.Equal("source", snapshot.GetProperty("kind").GetString());
                Assert.True(snapshot.GetProperty("fresh").GetBoolean());
                Assert.False(string.IsNullOrWhiteSpace(snapshot.GetProperty("fingerprint").GetString()));
            }
            else
            {
                Assert.Equal("unavailable", snapshot.GetProperty("kind").GetString());
                Assert.False(snapshot.GetProperty("fresh").GetBoolean());
            }

        }
        finally
        {
            TryKillDaemon(daemonPid);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ProjectTargetHealth_NonPositiveDiagnosticLimit_IsRejectedThroughDaemon(int maxDiagnostics)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(240));
        using var endpointLease = await DaemonProcessContractHarness
            .AcquireEndpointAsync(cancellation.Token)
            .ConfigureAwait(false);
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var solutionPath = fixture.SolutionPath;
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"ThinClientHealthValidationContract\",\"version\":\"1\"}}}",
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
            JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/call",
                @params = new
                {
                    name = "get_server_health",
                    arguments = new
                    {
                        targetPath = solutionPath,
                        includeDiagnostics = true,
                        maxDiagnostics,
                    },
                },
            }),
        };

        var result = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
            solutionPath,
            frames,
            new McpRawWireRunOptions
            {
                NoDaemon = false,
                DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
            });

        Assert.Equal(0, result.ExitCode);
        var response = McpRawWireTestHarness.FindResponse(result.StdoutLines, 3);
        var payload = response.GetProperty("result");
        Assert.False(payload.TryGetProperty("isError", out var isError) && isError.GetBoolean(), response.ToString());
        Assert.Equal("INVALID_ARGUMENT", payload.GetProperty("structuredContent").GetProperty("code").GetString());
        Assert.Equal("$.maxDiagnostics", payload.GetProperty("structuredContent").GetProperty("fieldPath").GetString());
        Assert.Contains(
            "INVALID_ARGUMENT",
            payload.GetProperty("content")[0].GetProperty("text").GetString(),
            StringComparison.Ordinal);
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
