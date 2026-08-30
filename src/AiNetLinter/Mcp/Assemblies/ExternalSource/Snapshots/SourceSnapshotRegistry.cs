#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

internal sealed class SourceSnapshotRegistry : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, SourceSnapshotEntry> snapshots = new(StringComparer.Ordinal);
    private readonly ExternalResourceRegistry resources;
    private readonly bool ownsResources;
    private int disposed;

    internal SourceSnapshotRegistry(ExternalResourceRegistry? resources = null)
    {
        this.resources = resources ?? new ExternalResourceRegistry();
        ownsResources = resources is null;
    }

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

    internal ExternalResourceHealthSnapshot Health => resources.Health;

    internal ExternalResourceOperationLease BeginOperation(CancellationToken cancellationToken)
    {
        if (resources.TryBeginOperation(cancellationToken, out var operation))
        {
            return operation!;
        }

        throw new ExternalResourceCapacityException(
            resources.Health.LastFailureReason
            ?? "Das externe Parallelitätsbudget ist ausgeschöpft.");
    }

    internal SourceSnapshotLease Acquire(ExternalSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var resolution = ResolveAcquire(snapshot);
        var failures = DisposeEntries(resolution.EvictedEntries);
        AddDisposeFailure(failures, resolution.Duplicate);
        if (!resolution.ResourceResult.Succeeded)
        {
            ThrowFailuresOrCapacity(failures, resolution.ResourceResult.FailureReason!);
        }

        if (failures.Count > 0)
        {
            CleanupFailedAcquire(resolution, failures);
            throw CreateDisposeException(failures);
        }

        return new SourceSnapshotLease(
            this,
            resolution.Entry!.Snapshot,
            resolution.ConsumerLease);
    }

    private AcquireResolution ResolveAcquire(ExternalSourceSnapshot snapshot)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (snapshots.TryGetValue(snapshot.Identity.StableValue, out var resident))
            {
                return AcquireResident(snapshot, resident);
            }

            return AcquireNew(snapshot);
        }
    }

    private AcquireResolution AcquireResident(
        ExternalSourceSnapshot snapshot,
        SourceSnapshotEntry resident)
    {
        var resourceResult = resources.TryAcquire(new ExternalResourceRequest(
            resident.ResourceIdentity,
            resident.Snapshot.ResourceUsage.DiskBytes,
            resident.Snapshot.ResourceUsage.MemoryBytes));
        if (!resourceResult.Succeeded)
        {
            return new(null, snapshot, resourceResult, null, false);
        }

        resident.LeaseCount++;
        return new(
            resident,
            ReferenceEquals(snapshot, resident.Snapshot) ? null : snapshot,
            resourceResult,
            resourceResult.Lease,
            false);
    }

    private AcquireResolution AcquireNew(ExternalSourceSnapshot snapshot)
    {
        var acquisition = resources.TryAcquireWithEvictions(new ExternalResourceRequest(
            snapshot.Identity.StableValue,
            snapshot.ResourceUsage.DiskBytes,
            snapshot.ResourceUsage.MemoryBytes));
        var evictedEntries = RemoveEntriesNoLock(acquisition.EvictedIdentities);
        if (!acquisition.Result.Succeeded)
        {
            return new(null, snapshot, acquisition.Result, null, false, evictedEntries);
        }

        var entry = new SourceSnapshotEntry(snapshot);
        snapshots.Add(snapshot.Identity.StableValue, entry);
        return new(entry, null, acquisition.Result, acquisition.Result.Lease, true, evictedEntries);
    }

    private void CleanupFailedAcquire(
        AcquireResolution resolution,
        List<Exception> failures)
    {
        AddDisposeFailure(failures, resolution.ConsumerLease);
        if (!resolution.IsNewEntry || resolution.Entry is null)
        {
            if (resolution.Entry is not null)
            {
                ReleaseResidentLease(resolution.Entry, failures);
            }

            return;
        }

        lock (gate)
        {
            if (snapshots.TryGetValue(resolution.Entry.ResourceIdentity, out var resident)
                && ReferenceEquals(resident, resolution.Entry))
            {
                snapshots.Remove(resolution.Entry.ResourceIdentity);
            }
        }

        resources.Remove(resolution.Entry.ResourceIdentity);
        AddDisposeFailure(failures, resolution.Entry.Snapshot);
    }

    private void ReleaseResidentLease(SourceSnapshotEntry entry, List<Exception> failures)
    {
        SourceSnapshotEntry? entryToDispose = null;
        lock (gate)
        {
            if (snapshots.TryGetValue(entry.ResourceIdentity, out var resident)
                && ReferenceEquals(resident, entry)
                && resident.LeaseCount > 0)
            {
                resident.LeaseCount--;
                if (Volatile.Read(ref disposed) != 0 && resident.LeaseCount == 0)
                {
                    snapshots.Remove(entry.ResourceIdentity);
                    entryToDispose = resident;
                }
            }
        }

        if (entryToDispose is not null)
        {
            failures.AddRange(DisposeEntries([entryToDispose]));
        }
    }

    private sealed record AcquireResolution(
        SourceSnapshotEntry? Entry,
        ExternalSourceSnapshot? Duplicate,
        ExternalResourceAcquireResult ResourceResult,
        ExternalResourceLease? ConsumerLease,
        bool IsNewEntry,
        IReadOnlyList<SourceSnapshotEntry>? evictedEntries = null)
    {
        internal IReadOnlyList<SourceSnapshotEntry> EvictedEntries { get; } =
            evictedEntries ?? Array.Empty<SourceSnapshotEntry>();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        List<SourceSnapshotEntry> remaining;
        lock (gate)
        {
            remaining = new List<SourceSnapshotEntry>(snapshots.Count);
            foreach (var pair in snapshots.ToList())
            {
                if (pair.Value.LeaseCount == 0)
                {
                    remaining.Add(pair.Value);
                    snapshots.Remove(pair.Key);
                }
            }
        }

        remaining.Sort(static (left, right) =>
            string.CompareOrdinal(left.Snapshot.Identity.StableValue, right.Snapshot.Identity.StableValue));
        var failures = DisposeEntries(remaining);

        if (ownsResources)
        {
            try
            {
                resources.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        DisposeFailureAggregator.ThrowIfAny(failures);
    }

    internal int EvictIdle()
    {
        var identities = resources.EvictIdleIdentities();
        List<SourceSnapshotEntry> evicted;
        lock (gate)
        {
            evicted = RemoveEntriesNoLock(identities);
        }

        var failures = DisposeEntries(evicted);
        DisposeFailureAggregator.ThrowIfAny(failures);
        return evicted.Count;
    }

    internal void Release(ExternalSourceSnapshot snapshot)
    {
        SourceSnapshotEntry? entryToDispose = null;
        lock (gate)
        {
            if (snapshots.TryGetValue(snapshot.Identity.StableValue, out var entry)
                && ReferenceEquals(entry.Snapshot, snapshot)
                && entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
                if (Volatile.Read(ref disposed) != 0 && entry.LeaseCount == 0)
                {
                    snapshots.Remove(snapshot.Identity.StableValue);
                    entryToDispose = entry;
                }
            }
        }

        if (entryToDispose is not null)
        {
            var failures = DisposeEntries([entryToDispose]);
            DisposeFailureAggregator.ThrowIfAny(failures);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SourceSnapshotRegistry));
        }
    }

    private List<SourceSnapshotEntry> RemoveEntriesNoLock(IEnumerable<string> identities)
    {
        var removed = new List<SourceSnapshotEntry>();
        foreach (var identity in identities.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (snapshots.Remove(identity, out var entry))
            {
                removed.Add(entry);
            }
        }

        return removed;
    }

    private List<Exception> DisposeEntries(IEnumerable<SourceSnapshotEntry> entries)
    {
        var failures = new List<Exception>();
        foreach (var entry in entries)
        {
            resources.Remove(entry.ResourceIdentity);

            try
            {
                entry.Snapshot.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static void AddDisposeFailure(List<Exception> failures, IDisposable? disposable)
    {
        if (disposable is null) return;
        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void ThrowFailuresOrCapacity(List<Exception> failures, string reason)
    {
        if (failures.Count > 0)
        {
            throw CreateDisposeException(failures);
        }

        throw new ExternalResourceCapacityException(reason);
    }

    private static Exception CreateDisposeException(List<Exception> failures) =>
        failures.Count == 1 ? failures[0] : new AggregateException(failures);

    private sealed class SourceSnapshotEntry(
        ExternalSourceSnapshot snapshot)
    {
        internal ExternalSourceSnapshot Snapshot { get; } = snapshot;

        internal string ResourceIdentity => Snapshot.Identity.StableValue;

        internal int LeaseCount { get; set; } = 1;
    }

}

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
