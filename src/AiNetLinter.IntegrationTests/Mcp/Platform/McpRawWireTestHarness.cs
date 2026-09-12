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
using AiNetLinter.IntegrationTests.Mcp.Daemon;
using AiNetLinter.IntegrationTests.Platform;
namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal sealed record McpRawWireRunResult(
    IReadOnlyList<string> StdoutLines,
    string StderrText,
    int ExitCode);

internal sealed record McpRawWireRunOptions
{
    internal TimeSpan? InterFrameDelay { get; init; }
    internal bool NoDaemon { get; init; } = true;
    internal double? DaemonIdleExitMinutes { get; init; }
    internal string? LocalAppDataOverride { get; init; }
    internal string? DaemonInstance { get; init; }
}

internal static class McpRawWireTestHarness
{
    private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(30);
    // Nach allen erwarteten Frames ist der Wire-Vertrag erfüllt. Ein noch offener
    // Server darf den Testhost nicht minutenlang festhalten oder Build-Artefakte sperren.
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StderrDrainTimeout = TimeSpan.FromSeconds(2);
    private const string InitializeProtocolVersion = "2024-11-05";
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
                    protocolVersion = InitializeProtocolVersion,
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

    internal static JsonElement FindResponseWithStructuredProperty(
        IEnumerable<string> lines,
        int id,
        string propertyName)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var responseId)
                || responseId.ValueKind != JsonValueKind.Number
                || responseId.GetInt32() != id
                || !root.TryGetProperty("result", out var result)
                || !result.TryGetProperty("structuredContent", out var structured)
                || !structured.TryGetProperty(propertyName, out _)) continue;

            return root.Clone();
        }

        throw new InvalidOperationException(
            $"Keine JSON-RPC-Antwort mit structuredContent.{propertyName} fuer id={id} gefunden.");
    }

    internal static async Task<List<string>> RunAndCollectStdoutAsync(
        string targetPath,
        string[] frames,
        McpRawWireRunOptions? options = null)
    {
        var result = await RunAndCollectWithDiagnosticsAsync(
            targetPath,
            frames,
            options).ConfigureAwait(false);
        return result.StdoutLines.ToList();
    }

    internal static async Task<McpRawWireRunResult> RunAndCollectWithDiagnosticsAsync(
        string targetPath,
        string[] frames,
        McpRawWireRunOptions? options = null)
    {
        options ??= new McpRawWireRunOptions();
        McpFixtureTargetSetup.Ensure(targetPath);
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(CancellationToken.None);
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}. " +
                "Test laeuft nur nach dotnet build.");
        }

        var psi = CreateProcessStartInfo(exePath, targetPath, options);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("AiNetLinter-Subprozess konnte nicht gestartet werden.");

        var stderrTask = process.StandardError.ReadToEndAsync();
        var expectedResponses = CountExpectedResponses(frames);
        var writer = process.StandardInput;
        var writerTask = WriteFramesAsync(writer, frames, targetPath, options.InterFrameDelay);

        var observed = new List<string>();
        var responseTimeout = GetResponseTimeout(frames.Length, options.InterFrameDelay);
        using var responseCancellation = new CancellationTokenSource();
        var readResponses = ReadStdoutFramesAsync(
            process.StandardOutput,
            observed,
            expectedResponses,
            responseCancellation.Token);
        var receivedExpectedResponses = await Task.WhenAny(
            readResponses,
            Task.Delay(responseTimeout)).ConfigureAwait(false) == readResponses;
        if (receivedExpectedResponses)
        {
            try { await readResponses.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        else
        {
            // Named-pipe stdout kann einen CancellationToken erst nach einem weiteren
            // Prozessereignis beobachten. Der externe Timeout bleibt daher der
            // verbindliche Hang-Schutz; der nachfolgende Prozess-Kill loest den Read.
            responseCancellation.Cancel();
        }

        try { await writerTask; } catch { /* Pipe-Close-Fehler ist hier unkritisch. */ }
        try { writer.Close(); } catch { /* Pipe evtl. schon geschlossen. */ }

        var (exitCode, stderrText) = await EnsureProcessTerminatedAsync(process, stderrTask).ConfigureAwait(false);
        if (!receivedExpectedResponses)
        {
            await Task.WhenAny(readResponses, Task.Delay(ProcessExitTimeout)).ConfigureAwait(false);
        }

        return new McpRawWireRunResult(observed, stderrText, exitCode);
    }

    private static ProcessStartInfo CreateProcessStartInfo(
        string exePath,
        string targetPath,
        McpRawWireRunOptions options)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(targetPath))!,
        };
        psi.ArgumentList.Add("--mcp-server");
        // Der Raw-Wire-Harness nutzt bewusst den ThinClient; ein kurzer Idle-Exit
        // verhindert, dass der testweise Daemon nach dem Prozessende Build-Artefakte sperrt.
        if (options.NoDaemon)
        {
            psi.Environment["AINETLINTER_NO_DAEMON"] = "1";
        }
        else
        {
            psi.ArgumentList.Add("--mcp-daemon-idle-exit-minutes");
            psi.ArgumentList.Add((options.DaemonIdleExitMinutes ?? 0.01).ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--daemon-instance");
            psi.ArgumentList.Add(options.DaemonInstance ?? DaemonEndpointJanitor.TestDaemonInstance);
        }

        if (options.LocalAppDataOverride is not null)
        {
            // Isoliert den daemonseitigen MRU-/State-Pfad vom echten Benutzerprofil;
            // der detached Spawn erbt die Umgebung des Thin-Clients.
            Directory.CreateDirectory(options.LocalAppDataOverride);
            psi.Environment["LOCALAPPDATA"] = options.LocalAppDataOverride;
        }

        return psi;
    }

    private static async Task WriteFramesAsync(
        StreamWriter writer,
        IEnumerable<string> frames,
        string targetPath,
        TimeSpan? interFrameDelay)
    {
        foreach (var frame in frames)
        {
            await writer.WriteLineAsync(AddTargetToToolCall(frame, targetPath));
            await writer.FlushAsync();
            if (interFrameDelay is { } delay)
            {
                await Task.Delay(delay);
            }
        }
    }

    private static int CountExpectedResponses(IEnumerable<string> frames) =>
        frames.Count(frame => frame.Contains("\"id\":", StringComparison.Ordinal));

    private static TimeSpan GetResponseTimeout(int frameCount, TimeSpan? interFrameDelay)
    {
        if (interFrameDelay is not { } delay || delay <= TimeSpan.Zero)
        {
            return DefaultResponseTimeout;
        }

        var scheduledWriteDuration = TimeSpan.FromTicks(delay.Ticks * frameCount);
        return scheduledWriteDuration > DefaultResponseTimeout
            ? scheduledWriteDuration + DefaultResponseTimeout
            : DefaultResponseTimeout;
    }

    private static string AddTargetToToolCall(string frame, string targetPath)
    {
        try
        {
            var root = JsonNode.Parse(frame)?.AsObject();
            if (root is null || !string.Equals(root["method"]?.GetValue<string>(), "tools/call", StringComparison.Ordinal))
                return frame;

            var parameters = root["params"]?.AsObject();
            var toolName = parameters?["name"]?.GetValue<string>();
            if (toolName is "get_server_health" or "report_observability_feedback")
                return frame;

            var arguments = parameters?["arguments"]?.AsObject();
            if (arguments is null || arguments.ContainsKey("targetPath")) return frame;

            arguments["targetPath"] = Path.GetFullPath(targetPath);
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

    private static async Task<(int ExitCode, string StderrText)> EnsureProcessTerminatedAsync(
        Process process,
        Task<string> stderrTask)
    {
        if (!process.HasExited)
        {
            await WaitOrKillAsync(process).ConfigureAwait(false);
        }

        var stderrText = string.Empty;
        var completed = await Task.WhenAny(stderrTask, Task.Delay(StderrDrainTimeout)).ConfigureAwait(false);
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

    private static async Task WaitOrKillAsync(Process process)
    {
        try
        {
            var exit = process.WaitForExitAsync();
            if (await Task.WhenAny(exit, Task.Delay(ProcessExitTimeout)).ConfigureAwait(false) != exit && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await Task.WhenAny(exit, Task.Delay(ProcessExitTimeout)).ConfigureAwait(false);
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
