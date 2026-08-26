#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;
namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal sealed record McpRawWireRunResult(
    IReadOnlyList<string> StdoutLines,
    string StderrText,
    int ExitCode);

internal static class McpRawWireTestHarness
{
    private const string LegacyProtocolVersion = "2024-11-05";
    private const string ModernProtocolVersion = "2026-07-28";
    private const string ClientName = "FramingTestClient";
    private const string ClientVersion = "1.0.0";

    internal static string[] BuildDiscoveryFrames(bool modern)
    {
        var meta = new Dictionary<string, object?>
        {
            ["io.modelcontextprotocol/protocolVersion"] = ModernProtocolVersion,
            ["io.modelcontextprotocol/clientInfo"] = new { name = ClientName, version = ClientVersion },
            ["io.modelcontextprotocol/clientCapabilities"] = new { },
        };
        var discoveryFrame = modern
            ? JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "server/discover",
                @params = new { _meta = meta },
            })
            : JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = LegacyProtocolVersion,
                    capabilities = new { },
                    clientInfo = new { name = ClientName, version = ClientVersion },
                },
            });
        var toolsListFrame = modern
            ? JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { _meta = meta },
            })
            : JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/list" });
        var initializedFrame = JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" });
        return modern
            ? new[] { discoveryFrame, toolsListFrame }
            : new[] { discoveryFrame, initializedFrame, toolsListFrame };
    }

    internal static JsonElement FindResponse(IEnumerable<string> lines, int id)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("id", out var responseId) &&
                responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
            {
                return document.RootElement.Clone();
            }
        }

        throw new InvalidOperationException($"Keine JSON-RPC-Antwort fuer id={id} gefunden.");
    }

    internal static async Task<List<string>> RunAndCollectStdoutAsync(
        string targetDirectory,
        string[] frames,
        TimeSpan? interFrameDelay = null,
        bool noDaemon = true)
    {
        var result = await RunAndCollectWithDiagnosticsAsync(
            targetDirectory,
            frames,
            interFrameDelay,
            noDaemon).ConfigureAwait(false);
        return result.StdoutLines.ToList();
    }

    internal static async Task<McpRawWireRunResult> RunAndCollectWithDiagnosticsAsync(
        string targetDirectory,
        string[] frames,
        TimeSpan? interFrameDelay = null,
        bool noDaemon = true,
        double? daemonIdleExitMinutes = null,
        string? localAppDataOverride = null)
    {
        McpFixtureProjectDefinition.Ensure(targetDirectory);
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(CancellationToken.None);
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}. " +
                "Test laeuft nur nach dotnet build.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = targetDirectory,
        };
        psi.ArgumentList.Add("--mcp-server");
        // Der Raw-Wire-Harness nutzt bewusst den ThinClient; ein kurzer Idle-Exit
        // verhindert, dass der testweise Daemon nach dem Prozessende Build-Artefakte sperrt.
        if (noDaemon)
        {
            psi.Environment["AINETLINTER_NO_DAEMON"] = "1";
        }
        else
        {
            psi.ArgumentList.Add("--mcp-daemon-idle-exit-minutes");
            psi.ArgumentList.Add((daemonIdleExitMinutes ?? 0.01).ToString(CultureInfo.InvariantCulture));
        }

        if (localAppDataOverride is not null)
        {
            // Isoliert den daemonseitigen MRU-/State-Pfad vom echten Benutzerprofil;
            // der detached Spawn erbt die Umgebung des Thin-Clients.
            Directory.CreateDirectory(localAppDataOverride);
            psi.Environment["LOCALAPPDATA"] = localAppDataOverride;
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("AiNetLinter-Subprozess konnte nicht gestartet werden.");

        var stderrTask = process.StandardError.ReadToEndAsync();
        var expectedResponses = CountExpectedResponses(frames);
        var writer = process.StandardInput;
        var writerTask = Task.Run(async () =>
        {
            foreach (var frame in frames)
            {
                await writer.WriteLineAsync(AddProjectRootToToolCall(frame, targetDirectory));
                await writer.FlushAsync();
                if (interFrameDelay is { } delay)
                {
                    await Task.Delay(delay);
                }
            }
        });

        var observed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await ReadStdoutFramesAsync(process.StandardOutput, observed, expectedResponses, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Server-Timeout: bereits gelesene Frames werden fuer die Assertions erhalten.
        }

        try { await writerTask; } catch { /* Pipe-Close-Fehler ist hier unkritisch. */ }
        try { writer.Close(); } catch { /* Pipe evtl. schon geschlossen. */ }

        try
        {
            await DrainRemainingStdoutAsync(process.StandardOutput, observed, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout beim Drain: der Prozess wird im Anschluss sicher beendet.
        }

        var (exitCode, stderrText) = await EnsureProcessTerminatedAsync(process, stderrTask).ConfigureAwait(false);
        return new McpRawWireRunResult(observed, stderrText, exitCode);
    }

    private static int CountExpectedResponses(IEnumerable<string> frames) =>
        frames.Count(frame => frame.Contains("\"id\":", StringComparison.Ordinal));

    private static string AddProjectRootToToolCall(string frame, string projectRoot)
    {
        try
        {
            var root = JsonNode.Parse(frame)?.AsObject();
            if (root is null || !string.Equals(root["method"]?.GetValue<string>(), "tools/call", StringComparison.Ordinal))
                return frame;

            var parameters = root["params"]?.AsObject();
            if (string.Equals(parameters?["name"]?.GetValue<string>(), "get_server_health", StringComparison.Ordinal))
                return frame;

            var arguments = parameters?["arguments"]?.AsObject();
            if (arguments is null || arguments.ContainsKey("projectRoot")) return frame;

            arguments["projectRoot"] = projectRoot;
            return root.ToJsonString();
        }
        catch (JsonException)
        {
            return frame;
        }
        catch (InvalidOperationException)
        {
            return frame;
        }
    }

    private static async Task ReadStdoutFramesAsync(
        StreamReader stdout, List<string> observed, int expectedResponses, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await stdout.ReadLineAsync(ct);
            if (line is null) break;
            observed.Add(line);
            if (expectedResponses > 0 && observed.Count >= expectedResponses)
            {
                break;
            }
        }
    }

    private static async Task DrainRemainingStdoutAsync(
        StreamReader stdout, List<string> observed, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await stdout.ReadLineAsync(ct);
            if (line is null) break;
            observed.Add(line);
        }
    }

    private static async Task<(int ExitCode, string StderrText)> EnsureProcessTerminatedAsync(
        Process process,
        Task<string> stderrTask)
    {
        if (!process.HasExited)
        {
            TryWaitOrKill(process);
        }

        var stderrText = string.Empty;
        var completed = await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        if (completed == stderrTask)
        {
            stderrText = await stderrTask.ConfigureAwait(false);
        }

        try
        {
            return (process.ExitCode, stderrText);
        }
        catch (InvalidOperationException)
        {
            return (int.MinValue, stderrText);
        }
    }

    private static void TryWaitOrKill(Process process)
    {
        try
        {
            if (!process.WaitForExit(TimeSpan.FromSeconds(10)) && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort-Cleanup nach einem bereits beendeten oder gesperrten Prozess.
        }
    }
}

// Stellvertreterprozess fuer den Haenger-Pfad (Konzept B.6 „Stellvertreter statt
// Injektion"): laeuft lange genug, ist per Welcome-PID identifizierbar und vom
// Client deterministisch kill-bar. Bewusst hier im Harness-Owner, damit alle
// Process.Start-Callsites in der Guard-Whitelist bleiben.
internal sealed class StandInProcess : IDisposable
{
    public StandInProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 30 127.0.0.1 > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Stellvertreterprozess konnte nicht gestartet werden.");
    }

    public Process Process { get; }

    public async Task<bool> WaitForExitAsync(TimeSpan limit)
    {
        var deadline = DateTime.UtcNow + limit;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.HasExited) return true;
            await Task.Delay(100).ConfigureAwait(false);
        }

        return Process.HasExited;
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            Process.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
            // Prozess war bereits vollstaendig beendet.
        }
        finally
        {
            Process.Dispose();
        }
    }
}
