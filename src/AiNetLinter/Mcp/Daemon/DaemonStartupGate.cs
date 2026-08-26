#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal static class DaemonStartupGate
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    internal static async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout)
        => await AcquireAsync(cancellationToken, timeout, DaemonProtocol.CurrentUserName).ConfigureAwait(false);

    internal static async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout,
        string userName)
        => await AcquireAsync(cancellationToken, timeout, userName, null).ConfigureAwait(false);

    internal static async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout,
        string userName,
        string? daemonInstance)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        var normalizedInstance = DaemonInstanceId.Normalize(daemonInstance);

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(timeout);

        var pipeName = DaemonProtocol.GetPipeName(userName, normalizedInstance);
        var semaphore = new Semaphore(1, 1, GetName(pipeName));
        try
        {
            while (true)
            {
                var acquired = semaphore.WaitOne(0);

                if (acquired)
                {
                    return new Lease(semaphore);
                }

                await Task.Delay(PollInterval, wait.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            semaphore.Dispose();
            throw new TimeoutException("Das Zeitlimit fuer den Daemon-Startup-Gate wurde ueberschritten.");
        }
        catch
        {
            semaphore.Dispose();
            throw;
        }
    }

    private static string GetName(string pipeName) =>
        $"Local\\AiNetLinter.Daemon.Start.{pipeName}";

    // Der benannte Windows-Semaphore ist selbst die einzige Prozess- und
    // Thread-uebergreifende Serialisierungsprimitive. Jeder Acquire-Aufruf besitzt
    // seinen eigenen Handle und gibt ihn mit dem Lease deterministisch frei; dadurch
    // entsteht kein unbounded lokaler Semaphore-Cache mit konkurrierender Entfernung.
    private sealed class Lease(Semaphore semaphore) : IAsyncDisposable
    {
        private int disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                semaphore.Release();
                semaphore.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }
}
