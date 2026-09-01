#nullable enable

using System;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

internal sealed class SourceSnapshotLease : IDisposable
{
    private readonly SourceSnapshotRegistry registry;
    private readonly ExternalSourceSnapshot snapshot;
    private readonly ExternalResourceLease? resourceLease;
    private int disposed;

    internal SourceSnapshotLease(
        SourceSnapshotRegistry registry,
        ExternalSourceSnapshot snapshot,
        ExternalResourceLease? resourceLease)
    {
        this.registry = registry;
        this.snapshot = snapshot;
        this.resourceLease = resourceLease;
    }

    internal ExternalSourceSnapshot Snapshot => snapshot;

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal SourceSnapshotLease AcquireSibling() => registry.Acquire(snapshot);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        resourceLease?.Dispose();
        registry.Release(snapshot);
    }
}
