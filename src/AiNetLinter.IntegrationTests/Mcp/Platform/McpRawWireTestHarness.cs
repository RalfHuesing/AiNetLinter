#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal static class McpRawWireTestHarness
{
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
        string targetDirectory, string[] frames, TimeSpan? interFrameDelay = null)
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

        await EnsureProcessTerminatedAsync(process, stderrTask);
        return observed;
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

    private static async Task EnsureProcessTerminatedAsync(Process process, Task stderrTask)
    {
        if (!process.HasExited)
        {
            TryWaitOrKill(process);
        }

        await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromSeconds(10)));
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
