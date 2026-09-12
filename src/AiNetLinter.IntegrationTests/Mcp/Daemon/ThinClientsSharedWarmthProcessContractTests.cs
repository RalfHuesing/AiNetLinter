#nullable enable

using System.Text.Json;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

[Trait("Category", "Integration")]
public sealed class ThinClientsSharedWarmthProcessContractTests
{
    private const int FirstHealthId = 3;
    // Der Wire-Vertrag braucht einen warmen zweiten Client, nicht vierzig identische
    // Polls. Zwanzig Sekunden decken den realen Roslyn-Load ab und halten den
    // unverzichtbaren Multiprozess-Test aus dem Runner-Hot-Path heraus.
    private const int LastHealthId = 22;
    private const int SecondaryLastHealthId = 8;
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(1);

    public ThinClientsSharedWarmthProcessContractTests(DaemonEndpointJanitorFixture janitor) => _ = janitor;

    [Fact]
    public async Task TwoThinClients_ConnectToSameDaemon_AndReuseWarmProjectKey()
    {
        // Endpunkt-Gate: der echte Daemon bindet den pro Testprozess isolierten Pipe-Namen.
        var gate = await DaemonProcessContractHarness.AcquireEndpointAsync(CancellationToken.None).ConfigureAwait(false);
        var sharedPid = 0;
        try
        {
            using var fixture = new SymbolGraphMiniFixtureWorkspace();
            var solutionPath = fixture.SolutionPath;
            using var isolatedState = TestTempDirectory.Create("thin-client-shared-state-");
            var clientFrames = CreateClientFrames(solutionPath);
            var daemonSpec = new DaemonProcessSpec(
                fixture.SolutionPath,
                isolatedState.DirectoryPath,
                IdleExitMinutes: 5);

            // Der lange Idle-Exit haelt den Daemon zwischen beiden Clients am Leben;
            // das Teardown killt ihn anhand der Welcome-PID, bevor das Fixture freigegeben wird.
            // Bewusst sequenziell: Client B muss nach A laufen, damit die Server-Uptime
            // streng weitergewachsen ist (Beweis fuer dieselbe warme Instanz).
            var first = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.SolutionPath,
                clientFrames,
                new McpRawWireRunOptions
                {
                    InterFrameDelay = PollDelay,
                    NoDaemon = false,
                    DaemonIdleExitMinutes = 5,
                    LocalAppDataOverride = isolatedState.DirectoryPath,
                    DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
                }).ConfigureAwait(false);
            // Die Welcome-PID gehoert zur Pipe, nicht zum oeffentlichen Health-Vertrag.
            // Sie muss vor den nachfolgenden Assertions feststehen, damit ein fehlschlagender
            // Wire-Contract den langlebigen Testdaemon niemals verwaist zuruecklaesst.
            sharedPid = await DaemonProcessContractHarness
                .GetDaemonProcessIdAsync(daemonSpec, CancellationToken.None)
                .ConfigureAwait(false);
            var second = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.SolutionPath,
                CreateClientFrames(solutionPath, primary: false),
                new McpRawWireRunOptions
                {
                    InterFrameDelay = TimeSpan.FromMilliseconds(400),
                    NoDaemon = false,
                    DaemonIdleExitMinutes = 5,
                    LocalAppDataOverride = isolatedState.DirectoryPath,
                    DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
                }).ConfigureAwait(false);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);

            var secondDaemonPid = await DaemonProcessContractHarness
                .GetDaemonProcessIdAsync(daemonSpec, CancellationToken.None)
                .ConfigureAwait(false);
            var healthFirst = LatestLoadedTargetHealthOrThrow(first.StdoutLines, LastHealthId);
            var healthSecond = LatestLoadedTargetHealthOrThrow(second.StdoutLines, SecondaryLastHealthId);

            Assert.True(sharedPid > 0);
            Assert.Equal(sharedPid, secondDaemonPid);

            var entryFirst = healthFirst.GetProperty("project");
            var entrySecond = healthSecond.GetProperty("project");
            Assert.Equal(solutionPath, entryFirst.GetProperty("targetPath").GetString(), ignoreCase: true);
            Assert.Equal(solutionPath, entrySecond.GetProperty("targetPath").GetString(), ignoreCase: true);

            // Shared-Warmth (B.6): beide Clients treffen dieselbe residente Projekt-Instanz —
            // kein zweiter vollstaendiger Load und kein Refresh dazwischen (identischer
            // Refresh-Zaehler). Die target-gebundene Projektion exponiert absichtlich
            // keine daemonweite Uptime; die identische Pipe-Welcome-PID beweist die Instanz.
            var refreshFirst = entryFirst.GetProperty("refreshCount").GetInt32();
            var refreshSecond = entrySecond.GetProperty("refreshCount").GetInt32();
            Assert.Equal(refreshFirst, refreshSecond);

        }
        finally
        {
            // Auch bei einem fruehen Assert muss der Daemon vor dem Fixture-Teardown
            // beendet werden, sonst sperrt er Build-Artefakte und den Endpoint.
            if (sharedPid > 0) TryKill(sharedPid);
            gate.Dispose();
        }
    }

    private static string[] CreateClientFrames(string targetPath, bool primary = true)
    {
        var lastHealthId = primary ? LastHealthId : SecondaryLastHealthId;
        var frames = new List<string>
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"SharedWarmth\",\"version\":\"1\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"get_file_skeleton\",\"arguments\":{\"filePaths\":[\"src/SymbolGraphMini/Greeter.cs\"]}}}",
        };
        for (var id = FirstHealthId; id <= lastHealthId; id++)
        {
            // Der zielgebundene Poll treibt das Laden genau dieses Projekts voran und
            // liefert dessen reduzierte, aber fuer Shared-Warmth ausreichende Projektion.
            var arguments = JsonSerializer.Serialize(new { targetPath });
            frames.Add(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"method\":\"tools/call\",\"params\":{\"name\":\"get_server_health\",\"arguments\":" + arguments + "}}");
        }

        return [.. frames];
    }

    private static JsonElement LatestLoadedTargetHealthOrThrow(IReadOnlyList<string> lines, int lastHealthId)
    {
        for (var id = lastHealthId; id >= FirstHealthId; id--)
        {
            var structured = StructuredContentOf(lines, id);
            var project = structured.GetProperty("project");
            if (string.Equals(project.GetProperty("loadState").GetString(), "Loaded", StringComparison.Ordinal))
            {
                return structured;
            }
        }

        throw new InvalidOperationException("Keine zielgebundene get_server_health-Antwort mit LoadState 'Loaded' gefunden.");
    }

    private static JsonElement StructuredContentOf(IReadOnlyList<string> lines, int id)
    {
        var response = McpRawWireTestHarness.FindResponse(lines, id);
        var result = response.GetProperty("result");
        Assert.False(
            result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            $"Tool-Aufruf id={id} lieferte einen Fehler: {response.ToString()}");
        return result.GetProperty("structuredContent");
    }

    private static void TryKill(int processId)
    {
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
            // Daemon hat sich bereits selbst beendet — nichts zu beenden.
        }
    }
}
