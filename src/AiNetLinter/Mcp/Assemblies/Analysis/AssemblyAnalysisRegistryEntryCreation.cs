#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisRegistryEntryCreation(
    CancellationTokenSource CancellationSource,
    Task<AssemblyAnalysisEntry> Task)
{
    private int cancellationSourceDisposed;

    internal void DisposeCancellationSource()
    {
        if (Interlocked.Exchange(ref cancellationSourceDisposed, 1) == 0)
        {
            CancellationSource.Dispose();
        }
    }
}

internal sealed class ExternalResourceCapacityException(string message) : Exception(message);

internal sealed class ExternalResourceLease : IDisposable
{
    private readonly ExternalResourceRegistry registry;
    private readonly string identity;
    private int disposed;

    internal ExternalResourceLease(ExternalResourceRegistry registry, string identity)
    {
        this.registry = registry;
        this.identity = identity;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) registry.Release(identity);
    }

    internal void DisposeAndRemove()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) registry.ReleaseAndRemove(identity);
    }
}

internal sealed class ExternalResourceReservation : IDisposable
{
    private readonly ExternalResourceRegistry registry;
    private readonly long diskBytes;
    private readonly long memoryBytes;
    private int disposed;

    internal ExternalResourceReservation(
        ExternalResourceRegistry registry,
        long diskBytes,
        long memoryBytes)
    {
        this.registry = registry;
        this.diskBytes = diskBytes;
        this.memoryBytes = memoryBytes;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            registry.ReleaseReservation(diskBytes, memoryBytes);
        }
    }
}

internal sealed class ExternalResourceOperationLease : IDisposable
{
    private readonly ExternalResourceRegistry registry;
    private int disposed;

    internal ExternalResourceOperationLease(ExternalResourceRegistry registry) => this.registry = registry;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) registry.EndOperation();
    }
}
