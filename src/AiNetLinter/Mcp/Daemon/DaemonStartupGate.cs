#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal static class DaemonStartupGate
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);
    private static readonly SemaphoreSlim LocalGate = new(1, 1);

    internal static async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout)
        => await AcquireAsync(cancellationToken, timeout, DaemonProtocol.CurrentUserName).ConfigureAwait(false);

    internal static async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout,
        string userName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        wait.CancelAfter(timeout);

        var semaphore = new Semaphore(1, 1, GetName(userName));
        var localAcquired = false;
        try
        {
            await LocalGate.WaitAsync(wait.Token).ConfigureAwait(false);
            localAcquired = true;
            while (true)
            {
                var acquired = semaphore.WaitOne(0);

                if (acquired)
                {
                    return new Lease(semaphore, LocalGate);
                }

                await Task.Delay(PollInterval, wait.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            semaphore.Dispose();
            if (localAcquired) LocalGate.Release();
            throw new TimeoutException("Das Zeitlimit fuer den Daemon-Startup-Gate wurde ueberschritten.");
        }
        catch
        {
            semaphore.Dispose();
            if (localAcquired) LocalGate.Release();
            throw;
        }
    }

    private static string GetName(string userName) =>
        $"Local\\AiNetLinter.Daemon.Start.{DaemonProtocol.GetPipeName(userName)}";

    private sealed class Lease(Semaphore semaphore, SemaphoreSlim localGate) : IAsyncDisposable
    {
        private int disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                try
                {
                    semaphore.Release();
                    semaphore.Dispose();
                }
                finally
                {
                    localGate.Release();
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
