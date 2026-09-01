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

internal sealed class AssemblyAnalysisRegistryRecoverableFailureException : Exception
{
    internal AssemblyAnalysisRegistryRecoverableFailureException(AssemblySessionFailure failure)
        : base(failure.Diagnostic.Message)
    {
        Failure = failure;
    }

    internal AssemblySessionFailure Failure { get; }
}

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
    private readonly ExternalResourceRequest request;
    private int state;

    private const int Active = 0;
    private const int Promoted = 1;
    private const int Disposed = 2;

    internal ExternalResourceReservation(
        ExternalResourceRegistry registry,
        ExternalResourceRequest request)
    {
        this.registry = registry;
        this.request = request;
    }

    internal ExternalResourceRequest Request => request;

    internal ExternalResourceRegistry Registry => registry;

    internal bool TryPromote() =>
        Interlocked.CompareExchange(ref state, Promoted, Active) == Active;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref state, Disposed, Active) == Active)
        {
            registry.ReleaseReservation(this);
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
