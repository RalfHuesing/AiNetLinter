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
    private const int LastHealthId = 42;
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
            using var isolatedState = TestTempDirectory.Create("thin-client-shared-state-");
            var clientFrames = CreateClientFrames();

            // Der lange Idle-Exit haelt den Daemon zwischen beiden Clients am Leben;
            // das Teardown killt ihn anhand der Welcome-PID, bevor das Fixture freigegeben wird.
            // Bewusst sequenziell: Client B muss nach A laufen, damit die Server-Uptime
            // streng weitergewachsen ist (Beweis fuer dieselbe warme Instanz).
            var first = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.RootPath,
                clientFrames,
                new McpRawWireRunOptions
                {
                    InterFrameDelay = PollDelay,
                    NoDaemon = false,
                    DaemonIdleExitMinutes = 5,
                    LocalAppDataOverride = isolatedState.DirectoryPath,
                    DaemonInstance = DaemonEndpointJanitor.TestDaemonInstance,
                }).ConfigureAwait(false);
            var second = await McpRawWireTestHarness.RunAndCollectWithDiagnosticsAsync(
                fixture.RootPath,
                CreateClientFrames(primary: false),
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

            var healthFirst = LatestLoadedHealthOrThrow(first.StdoutLines);
            var healthSecond = LatestLoadedHealthOrThrow(second.StdoutLines);
            var daemonFirst = healthFirst.GetProperty("daemon");
            var daemonSecond = healthSecond.GetProperty("daemon");
            Assert.Equal("daemon", daemonFirst.GetProperty("mode").GetString());
            Assert.Equal("daemon", daemonSecond.GetProperty("mode").GetString());

            sharedPid = daemonFirst.GetProperty("processId").GetInt32();
            Assert.True(sharedPid > 0);
            Assert.Equal(sharedPid, daemonSecond.GetProperty("processId").GetInt32());

            var projectsFirst = healthFirst.GetProperty("projects");
            var projectsSecond = healthSecond.GetProperty("projects");
            // Bewusst Root-Match statt Single(): Der geteilte Daemon darf auch
            // fremde Keys resident halten, solange der Fixture-Key geteilt wird.
            var entryFirst = SelectFixtureEntry(projectsFirst, fixture.RootPath);
            var entrySecond = SelectFixtureEntry(projectsSecond, fixture.RootPath);

            // Shared-Warmth (B.6): beide Clients treffen dieselbe residente Projekt-Instanz —
            // kein zweiter vollstaendiger Load und kein Refresh dazwischen (identischer
            // Refresh-Zaehler), waehrend die Instanz-Uptime strikt weitergelaufen ist.
            var refreshFirst = entryFirst.GetProperty("refreshCount").GetInt32();
            var refreshSecond = entrySecond.GetProperty("refreshCount").GetInt32();
            Assert.Equal(refreshFirst, refreshSecond);
            var uptimeFirst = entryFirst.GetProperty("uptimeSeconds").GetDouble();
            var uptimeSecond = entrySecond.GetProperty("uptimeSeconds").GetDouble();
            Assert.True(
                uptimeSecond > uptimeFirst,
                $"Instanz-Uptime nicht gewachsen: erster Client {uptimeFirst}s, zweiter Client {uptimeSecond}s.");

            var keys = daemonSecond.GetProperty("keys");
            Assert.Contains(
                keys.EnumerateArray(),
                key => string.Equals(key.GetString(), fixture.RootPath, StringComparison.OrdinalIgnoreCase));

        }
        finally
        {
            // Auch bei einem fruehen Assert muss der Daemon vor dem Fixture-Teardown
            // beendet werden, sonst sperrt er Build-Artefakte und den Endpoint.
            if (sharedPid > 0) TryKill(sharedPid);
            gate.Dispose();
        }
    }

    private static string[] CreateClientFrames(bool primary = true)
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
            frames.Add(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"method\":\"tools/call\",\"params\":{\"name\":\"get_server_health\",\"arguments\":{}}}");
        }

        return [.. frames];
    }

    private static JsonElement LatestLoadedHealthOrThrow(IReadOnlyList<string> lines)
    {
        // Fehlende Ids (kuerzere Secondary-Sequenz) werden einfach uebersprungen.
        for (var id = LastHealthId; id >= FirstHealthId; id--)
        {
            try
            {
                var structured = StructuredContentOf(lines, id);
                var projects = structured.GetProperty("projects");
                if (projects.GetArrayLength() > 0 &&
                    string.Equals(
                        projects[0].GetProperty("loadState").GetString(),
                        "Loaded",
                        StringComparison.Ordinal))
                {
                    return structured;
                }
            }
            catch (InvalidOperationException)
            {
                // Antwort zu dieser Id fehlt oder war unvollständig — aeltere pruefen.
            }
            catch (KeyNotFoundException)
            {
                // dito.
            }
        }

        throw new InvalidOperationException("Keine get_server_health-Antwort mit LoadState 'Loaded' gefunden.");
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

    private static JsonElement SelectFixtureEntry(JsonElement projects, string fixtureRoot)
    {
        foreach (var entry in projects.EnumerateArray())
        {
            if (string.Equals(
                    entry.GetProperty("projectRoot").GetString(),
                    fixtureRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        throw new InvalidOperationException($"Kein Projekt-Eintrag fuer '{fixtureRoot}' in der Health-Antwort.");
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
