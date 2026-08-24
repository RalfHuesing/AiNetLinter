#nullable enable

using System.Threading;

namespace AiNetLinter.Mcp.Daemon;

internal interface IDaemonInstanceLock : IDisposable
{
    bool TryAcquire();
}

internal sealed class DaemonInstanceLock : IDaemonInstanceLock
{
    private readonly Semaphore semaphore;
    private int acquired;

    internal DaemonInstanceLock(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        semaphore = new Semaphore(1, 1, $"Local\\AiNetLinter.Daemon.{pipeName}");
    }

    public bool TryAcquire()
    {
        if (Volatile.Read(ref acquired) != 0)
        {
            return true;
        }

        if (!semaphore.WaitOne(TimeSpan.Zero))
        {
            return false;
        }

        Volatile.Write(ref acquired, 1);
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref acquired, 0) != 0)
        {
            semaphore.Release();
        }

        semaphore.Dispose();
    }
}
