#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class SourceSnapshotRegistry : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, SourceSnapshotEntry> snapshots = new(StringComparer.Ordinal);
    private int disposed;

    internal int ResidentCount
    {
        get
        {
            lock (gate)
            {
                return snapshots.Count;
            }
        }
    }

    internal SourceSnapshotLease Acquire(ExternalSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SourceSnapshotEntry entry;
        ExternalSourceSnapshot? duplicate = null;
        lock (gate)
        {
            ThrowIfDisposed();
            if (snapshots.TryGetValue(snapshot.Identity.StableValue, out var resident))
            {
                entry = resident;
                entry.LeaseCount++;
                if (!ReferenceEquals(snapshot, resident.Snapshot))
                {
                    duplicate = snapshot;
                }
            }
            else
            {
                entry = new SourceSnapshotEntry(snapshot);
                entry.LeaseCount = 1;
                snapshots.Add(snapshot.Identity.StableValue, entry);
            }
        }

        duplicate?.Dispose();
        return new SourceSnapshotLease(this, entry.Snapshot);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        List<ExternalSourceSnapshot> remaining;
        lock (gate)
        {
            remaining = new List<ExternalSourceSnapshot>(snapshots.Count);
            foreach (var entry in snapshots.Values)
            {
                remaining.Add(entry.Snapshot);
            }

            snapshots.Clear();
        }

        remaining.Sort(static (left, right) =>
            string.CompareOrdinal(left.Identity.StableValue, right.Identity.StableValue));

        var failures = new List<Exception>();
        foreach (var snapshot in remaining)
        {
            try
            {
                snapshot.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        DisposeFailureAggregator.ThrowIfAny(failures);
    }

    internal void Release(ExternalSourceSnapshot snapshot)
    {
        lock (gate)
        {
            if (snapshots.TryGetValue(snapshot.Identity.StableValue, out var entry)
                && ReferenceEquals(entry.Snapshot, snapshot)
                && entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SourceSnapshotRegistry));
        }
    }

    private sealed class SourceSnapshotEntry(ExternalSourceSnapshot snapshot)
    {
        internal ExternalSourceSnapshot Snapshot { get; } = snapshot;

        internal int LeaseCount { get; set; }
    }

}

internal sealed class SourceSnapshotLease : IDisposable
{
    private readonly SourceSnapshotRegistry registry;
    private readonly ExternalSourceSnapshot snapshot;
    private int disposed;

    internal SourceSnapshotLease(SourceSnapshotRegistry registry, ExternalSourceSnapshot snapshot)
    {
        this.registry = registry;
        this.snapshot = snapshot;
    }

    internal ExternalSourceSnapshot Snapshot => snapshot;

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        registry.Release(snapshot);
    }
}
