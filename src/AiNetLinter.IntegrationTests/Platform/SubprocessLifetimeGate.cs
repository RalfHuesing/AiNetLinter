#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.IntegrationTests.Platform;

internal sealed class SubprocessLifetimeGate
{
    private readonly SemaphoreSlim semaphore;

    public SubprocessLifetimeGate(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(semaphore);
    }

    private sealed class Lease : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private int disposed;

        public Lease(SemaphoreSlim semaphore) => this.semaphore = semaphore;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) semaphore.Release();
        }
    }
}

internal static class SubprocessLifetimeBudget
{
    // xUnit laeuft mit bis zu vier parallelen Testfaellen; Daemon-Vertraege koennen
    // innerhalb eines Testfalls zwei langlebige Prozesse gleichzeitig halten.
    internal static SubprocessLifetimeGate Shared { get; } = new(capacity: 8);
}
