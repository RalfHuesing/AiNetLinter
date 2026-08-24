#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AiNetLinter.Mcp.Lifetime;

internal sealed class ParentProcessWatchdog : IAsyncDisposable
{
    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMilliseconds(500);
    private const string ParentProcessMessagePrefix = "Elternprozess ";

    private readonly int parentProcessId;
    private readonly CancellationTokenSource shutdownSource;
    private readonly CancellationTokenSource monitorSource = new();
    private readonly TimeSpan pollingInterval;
    private readonly Action<string>? report;
    private readonly Task monitorTask;
    private int shutdownRequested;
    private int disposed;

    private ParentProcessWatchdog(
        int parentProcessId,
        CancellationTokenSource shutdownSource,
        TimeSpan pollingInterval,
        Action<string>? report)
    {
        this.parentProcessId = parentProcessId;
        this.shutdownSource = shutdownSource;
        this.pollingInterval = pollingInterval;
        this.report = report;
        monitorTask = MonitorLoopAsync();
    }

    internal static ParentProcessWatchdog Start(
        int parentProcessId,
        CancellationTokenSource shutdownSource,
        TimeSpan? pollingInterval = null,
        Action<string>? report = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parentProcessId);
        ArgumentNullException.ThrowIfNull(shutdownSource);

        var effectiveInterval = pollingInterval ?? DefaultPollingInterval;
        if (effectiveInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingInterval));
        }

        Log.Debug("MCP-Parent-Watchdog gestartet (ParentPid={ParentPid}, PollingMs={PollingMs})", parentProcessId, effectiveInterval.TotalMilliseconds);
        return new ParentProcessWatchdog(parentProcessId, shutdownSource, effectiveInterval, report);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        monitorSource.Cancel();
        try
        {
            await monitorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        monitorSource.Dispose();
        Log.Debug("MCP-Parent-Watchdog beendet (ParentPid={ParentPid}, ShutdownRequested={ShutdownRequested})", parentProcessId, Volatile.Read(ref shutdownRequested) != 0);
    }

    private async Task MonitorLoopAsync()
    {
        try
        {
            // Initialer Check: Existiert der Elternprozess ueberhaupt?
            if (!IsProcessAlive(parentProcessId))
            {
                RequestShutdown($"{ParentProcessMessagePrefix}{parentProcessId} ist nicht vorhanden oder bereits beendet.");
                return;
            }

            while (!monitorSource.IsCancellationRequested)
            {
                await Task.Delay(pollingInterval, monitorSource.Token).ConfigureAwait(false);

                if (!IsProcessAlive(parentProcessId))
                {
                    RequestShutdown($"{ParentProcessMessagePrefix}{parentProcessId} wurde beendet.");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (monitorSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RequestShutdown($"{ParentProcessMessagePrefix}{parentProcessId} konnte nicht mehr ueberwacht werden: {exception.Message}");
        }
    }

    internal static bool IsProcessAlive(int processId)
    {
        if (processId <= 0) return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Prozess existiert nicht (mehr)
            return false;
        }
        catch (InvalidOperationException)
        {
            // Prozess ist bereits im Beendigungsvorgang
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            // Prozess existiert, Zugriff verweigert (z. B. System-Prozess) -> als lebendig werten
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RequestShutdown(string reason)
    {
        if (Interlocked.Exchange(ref shutdownRequested, 1) != 0) return;

        Log.Information("MCP-Parent-Watchdog loest Abbruch aus (ParentPid={ParentPid}, Grund={Reason})", parentProcessId, reason);
        report?.Invoke($"[INFO]: {reason} MCP-Server wird sauber beendet.");
        try
        {
            shutdownSource.Cancel();
        }
        catch (Exception exception)
        {
            report?.Invoke($"[WARN]: MCP-Server konnte nach Parent-Exit nicht abgebrochen werden: {exception.Message}");
        }
    }
}
