#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class ExternalResourceRegistryDefaults
{
    internal const long MaxDiskBytes = 512L * 1024 * 1024;
    internal const long MaxMemoryBytes = 512L * 1024 * 1024;
    internal const int MaxParallelOperations = 4;
    internal const int MaxResidentResources = 32;
    internal static readonly TimeSpan IdleTtl = TimeSpan.FromMinutes(45);
}

internal sealed record ExternalResourceRegistryOptions(
    long MaxDiskBytes = ExternalResourceRegistryDefaults.MaxDiskBytes,
    long MaxMemoryBytes = ExternalResourceRegistryDefaults.MaxMemoryBytes,
    int MaxParallelOperations = ExternalResourceRegistryDefaults.MaxParallelOperations,
    int MaxResidentResources = ExternalResourceRegistryDefaults.MaxResidentResources,
    TimeSpan IdleTtl = default,
    TimeProvider? Clock = null);

internal enum ExternalResourceHealth
{
    Healthy,
    Degraded,
    CapacityExceeded,
    Disposed,
}

internal sealed record ExternalResourceRequest(
    string Identity,
    long DiskBytes,
    long MemoryBytes);

internal sealed record ExternalResourceHealthSnapshot(
    ExternalResourceHealth Health,
    int ResidentResources,
    int MaxResidentResources,
    long DiskBytes,
    long MaxDiskBytes,
    long MemoryBytes,
    long MaxMemoryBytes,
    int ActiveOperations,
    int MaxParallelOperations,
    string? LastFailureReason);

internal sealed record ExternalResourceAcquireResult(
    ExternalResourceLease? Lease,
    ExternalResourceHealthSnapshot Health,
    string? FailureReason)
{
    internal bool Succeeded => Lease is not null;
}

/// <summary>
/// Separates budgets and lifetime accounting for external assemblies/source
/// resources from the four resident project contexts.
/// </summary>
internal sealed class ExternalResourceRegistry : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ResourceEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ExternalResourceRegistryOptions options;
    private readonly TimeProvider clock;
    private readonly TimeSpan idleTtl;
    private readonly SemaphoreSlim operationSlots;
    private string? lastFailureReason;
    private ExternalResourceHealth lastFailureHealth = ExternalResourceHealth.Healthy;
    private int activeOperations;
    private int disposed;

    internal ExternalResourceRegistry(ExternalResourceRegistryOptions? options = null)
    {
        this.options = options ?? new ExternalResourceRegistryOptions();
        ValidateOptions(this.options);
        clock = this.options.Clock ?? TimeProvider.System;
        idleTtl = this.options.IdleTtl > TimeSpan.Zero
            ? this.options.IdleTtl
            : ExternalResourceRegistryDefaults.IdleTtl;
        operationSlots = new SemaphoreSlim(this.options.MaxParallelOperations, this.options.MaxParallelOperations);
    }

    internal int ResidentCount
    {
        get { lock (gate) return entries.Count; }
    }

    internal TimeSpan IdleTtl => idleTtl;

    internal ExternalResourceHealthSnapshot Health
    {
        get
        {
            lock (gate) return CreateHealthNoLock(lastFailureHealth);
        }
    }

    internal ExternalResourceAcquireResult TryAcquire(ExternalResourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Identity)) throw new ArgumentException("Die Ressourcenidentität darf nicht leer sein.", nameof(request));
        if (request.DiskBytes < 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.MemoryBytes < 0) throw new ArgumentOutOfRangeException(nameof(request));

        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return FailureNoLock(ExternalResourceHealth.Disposed, "Das externe Ressourcenregister wurde bereits beendet.");
            }

            if (entries.TryGetValue(request.Identity, out var resident))
            {
                resident.LeaseCount++;
                resident.LastUsedUtc = UtcNowNoLock();
                ClearFailureNoLock();
                return new(new ExternalResourceLease(this, request.Identity), CreateHealthNoLock(ExternalResourceHealth.Healthy), null);
            }

            EvictExpiredNoLock(UtcNowNoLock());
            EvictLeastRecentlyUsedNoLock(request);
            var reason = CapacityReasonNoLock(request);
            if (reason is not null)
            {
                return FailureNoLock(ExternalResourceHealth.CapacityExceeded, reason);
            }

            entries.Add(request.Identity, new ResourceEntry(request.DiskBytes, request.MemoryBytes, UtcNowNoLock()));
            ClearFailureNoLock();
            return new(new ExternalResourceLease(this, request.Identity), CreateHealthNoLock(ExternalResourceHealth.Healthy), null);
        }
    }

    internal bool TryBeginOperation(CancellationToken cancellationToken, out ExternalResourceOperationLease? lease)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0)
        {
            lease = null;
            return false;
        }

        if (!operationSlots.Wait(0))
        {
            lock (gate)
            {
                lastFailureHealth = ExternalResourceHealth.Degraded;
                lastFailureReason = "Das externe Parallelitätsbudget ist ausgeschöpft.";
            }
            lease = null;
            return false;
        }

        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                operationSlots.Release();
                lease = null;
                return false;
            }

            activeOperations++;
            lease = new ExternalResourceOperationLease(this);
            return true;
        }
    }

    internal int EvictIdle()
    {
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return 0;
            return EvictExpiredNoLock(UtcNowNoLock());
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lock (gate)
        {
            entries.Clear();
            lastFailureReason = "Das externe Ressourcenregister wurde beendet.";
        }

        operationSlots.Dispose();
    }

    internal void Release(string identity)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(identity, out var entry) || entry.LeaseCount == 0) return;
            entry.LeaseCount--;
            entry.LastUsedUtc = UtcNowNoLock();
            if (Volatile.Read(ref disposed) != 0 && entry.LeaseCount == 0)
            {
                entries.Remove(identity);
            }
        }
    }

    internal void EndOperation()
    {
        lock (gate)
        {
            if (activeOperations > 0) activeOperations--;
            if (Volatile.Read(ref disposed) != 0) return;
        }

        try
        {
            operationSlots.Release();
        }
        catch (ObjectDisposedException)
        {
            // Dispose is terminal; no new operation can be admitted afterwards.
        }
    }

    private ExternalResourceAcquireResult FailureNoLock(ExternalResourceHealth health, string reason)
    {
        lastFailureHealth = health;
        lastFailureReason = reason;
        return new(null, CreateHealthNoLock(health), reason);
    }

    private void ClearFailureNoLock()
    {
        lastFailureHealth = ExternalResourceHealth.Healthy;
        lastFailureReason = null;
    }

    private void EvictLeastRecentlyUsedNoLock(ExternalResourceRequest request)
    {
        while (CapacityReasonNoLock(request) is not null)
        {
            var victim = entries
                .Where(pair => pair.Value.LeaseCount == 0)
                .OrderBy(pair => pair.Value.LastUsedUtc)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (victim.Key is null) return;
            entries.Remove(victim.Key);
        }
    }

    private string? CapacityReasonNoLock(ExternalResourceRequest request)
    {
        if (request.DiskBytes > options.MaxDiskBytes || Sum(entry => entry.DiskBytes) + request.DiskBytes > options.MaxDiskBytes)
        {
            return $"Das externe Diskbudget ist ausgeschöpft ({options.MaxDiskBytes} Bytes).";
        }

        if (request.MemoryBytes > options.MaxMemoryBytes || Sum(entry => entry.MemoryBytes) + request.MemoryBytes > options.MaxMemoryBytes)
        {
            return $"Das externe Speicherbudget ist ausgeschöpft ({options.MaxMemoryBytes} Bytes).";
        }

        return entries.Count >= options.MaxResidentResources
            ? $"Das externe Ressourcenlimit ist ausgeschöpft ({options.MaxResidentResources} Einträge)."
            : null;
    }

    private int EvictExpiredNoLock(DateTime now)
    {
        var expired = entries
            .Where(pair => pair.Value.LeaseCount == 0 && now - pair.Value.LastUsedUtc > idleTtl)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var identity in expired) entries.Remove(identity);
        return expired.Count;
    }

    private ExternalResourceHealthSnapshot CreateHealthNoLock(ExternalResourceHealth health)
    {
        if (health is ExternalResourceHealth.Healthy && lastFailureReason is not null)
        {
            health = ExternalResourceHealth.Degraded;
        }

        return new(
            health,
            entries.Count,
            options.MaxResidentResources,
            Sum(entry => entry.DiskBytes),
            options.MaxDiskBytes,
            Sum(entry => entry.MemoryBytes),
            options.MaxMemoryBytes,
            activeOperations,
            options.MaxParallelOperations,
            lastFailureReason);
    }

    private long Sum(Func<ResourceEntry, long> selector) => entries.Values.Sum(selector);

    private DateTime UtcNowNoLock() => clock.GetUtcNow().UtcDateTime;

    private static void ValidateOptions(ExternalResourceRegistryOptions value)
    {
        if (value.MaxDiskBytes <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxDiskBytes));
        if (value.MaxMemoryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxMemoryBytes));
        if (value.MaxParallelOperations <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxParallelOperations));
        if (value.MaxResidentResources <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxResidentResources));
        if (value.IdleTtl < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value.IdleTtl));
    }

    private sealed class ResourceEntry(long diskBytes, long memoryBytes, DateTime lastUsedUtc)
    {
        internal long DiskBytes { get; } = diskBytes;
        internal long MemoryBytes { get; } = memoryBytes;
        internal DateTime LastUsedUtc { get; set; } = lastUsedUtc;
        internal int LeaseCount { get; set; } = 1;
    }
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
