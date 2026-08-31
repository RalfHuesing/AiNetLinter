#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>
/// Trennt Budget- und Lebensdauerverwaltung externer Assembly-/Source-Ressourcen
/// von den vier residenten Projektkontexten.
/// </summary>
internal sealed class ExternalResourceRegistry : IDisposable
{
    private const string EmptyIdentityMessage = "Die Ressourcenidentität darf nicht leer sein.";
    private readonly Lock gate = new();
    private readonly Dictionary<string, ResourceEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ExternalResourceRegistryOptions options;
    private readonly TimeProvider clock;
    private readonly TimeSpan idleTtl;
    private readonly SemaphoreSlim operationSlots;
    private string? lastFailureReason;
    private ExternalResourceHealth lastFailureHealth = ExternalResourceHealth.Healthy;
    private int activeOperations;
    private long reservedDiskBytes;
    private long reservedMemoryBytes;
    private readonly Dictionary<string, int> reservedIdentityCounts = new(StringComparer.OrdinalIgnoreCase);
    private int disposed;

    internal ExternalResourceRegistry(ExternalResourceRegistryOptions? options = null)
    {
        this.options = options ?? new ExternalResourceRegistryOptions();
        ExternalResourceRegistrySupport.ValidateOptions(this.options);
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

    internal TimeProvider Clock => clock;

    internal ExternalResourceHealthSnapshot Health
    {
        get
        {
            lock (gate) return CreateHealthNoLock(lastFailureHealth);
        }
    }

    internal ExternalResourceAcquireResult TryAcquire(ExternalResourceRequest request)
    {
        return TryAcquireWithEvictions(request).Result;
    }

    internal ExternalResourceAcquireResult TryAcquireWithoutEvictions(ExternalResourceRequest request)
    {
        return TryAcquireCore(request, allowEvictions: false).Result;
    }

    internal (ExternalResourceAcquireResult Result, IReadOnlyList<string> EvictedIdentities)
        TryAcquireWithEvictions(ExternalResourceRequest request)
    {
        return TryAcquireCore(request, allowEvictions: true);
    }

    private (ExternalResourceAcquireResult Result, IReadOnlyList<string> EvictedIdentities)
        TryAcquireCore(ExternalResourceRequest request, bool allowEvictions)
    {
        ValidateRequest(request);

        lock (gate)
        {
            var evicted = new List<string>();
            if (Volatile.Read(ref disposed) != 0)
            {
                return (FailureNoLock(ExternalResourceHealth.Disposed, "Das externe Ressourcenregister wurde bereits beendet."), evicted);
            }

            if (entries.TryGetValue(request.Identity, out var resident))
            {
                resident.LeaseCount++;
                resident.LastUsedUtc = ExternalResourceRegistrySupport.UtcNow(clock);
                ClearFailureNoLock();
                return (new(new ExternalResourceLease(this, request.Identity), CreateHealthNoLock(ExternalResourceHealth.Healthy), null), evicted);
            }

            if (allowEvictions)
            {
                EvictExpiredNoLock(ExternalResourceRegistrySupport.UtcNow(clock), evicted);
                EvictLeastRecentlyUsedNoLock(request, evicted);
            }
            var reason = CapacityReasonNoLock(request);
            if (reason is not null)
            {
                return (FailureNoLock(ExternalResourceHealth.CapacityExceeded, reason), evicted);
            }

            entries.Add(request.Identity, new ResourceEntry(request.DiskBytes, request.MemoryBytes, ExternalResourceRegistrySupport.UtcNow(clock)));
            ClearFailureNoLock();
            return (new(new ExternalResourceLease(this, request.Identity), CreateHealthNoLock(ExternalResourceHealth.Healthy), null), evicted);
        }
    }

    internal bool HasCapacity(ExternalResourceRequest request)
    {
        ValidateRequest(request);

        lock (gate)
        {
            return Volatile.Read(ref disposed) == 0 && CapacityReasonNoLock(request) is null;
        }
    }

    internal bool CanAccommodate(ExternalResourceRequest request)
    {
        ValidateRequest(request);

        return request.DiskBytes <= options.MaxDiskBytes
            && request.MemoryBytes <= options.MaxMemoryBytes;
    }

    internal bool TryBeginOperation(CancellationToken cancellationToken, out ExternalResourceOperationLease? lease)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0)
        {
            lease = null;
            return false;
        }

        bool slotAcquired;
        try
        {
            slotAcquired = operationSlots.Wait(0);
        }
        catch (ObjectDisposedException)
        {
            lease = null;
            return false;
        }

        if (!slotAcquired)
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
                ExternalResourceRegistrySupport.ReleaseOperationSlot(operationSlots);
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
        return EvictIdleIdentities().Count;
    }

    internal IReadOnlyList<string> EvictIdleIdentities()
    {
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return Array.Empty<string>();
            var evicted = new List<string>();
            EvictExpiredNoLock(ExternalResourceRegistrySupport.UtcNow(clock), evicted);
            return evicted;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lock (gate)
        {
            entries.Clear();
            reservedIdentityCounts.Clear();
            reservedDiskBytes = 0;
            reservedMemoryBytes = 0;
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
            entry.LastUsedUtc = ExternalResourceRegistrySupport.UtcNow(clock);
            if (Volatile.Read(ref disposed) != 0 && entry.LeaseCount == 0)
            {
                entries.Remove(identity);
            }
        }
    }

    internal void ReleaseAndRemove(string identity)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(identity, out var entry) || entry.LeaseCount == 0)
            {
                return;
            }

            entry.LeaseCount--;
            entry.LastUsedUtc = ExternalResourceRegistrySupport.UtcNow(clock);
            if (entry.LeaseCount == 0)
            {
                entries.Remove(identity);
            }
        }
    }

    internal bool TryReserve(
        ExternalResourceRequest request,
        out ExternalResourceReservation? reservation,
        out string? failureReason)
    {
        var result = TryReserveWithEvictions(request);
        reservation = result.Reservation;
        failureReason = result.FailureReason;
        return result.Succeeded;
    }

    internal (bool Succeeded,
        ExternalResourceReservation? Reservation,
        string? FailureReason,
        IReadOnlyList<string> EvictedIdentities) TryReserveWithEvictions(
        ExternalResourceRequest request)
    {
        ValidateRequest(request);

        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return new(
                    false,
                    null,
                    "Das externe Ressourcenregister wurde bereits beendet.",
                    Array.Empty<string>());
            }

            var evicted = new List<string>();
            EvictExpiredNoLock(ExternalResourceRegistrySupport.UtcNow(clock), evicted);
            EvictLeastRecentlyUsedNoLock(request, evicted);
            var reason = CapacityReasonNoLock(request);
            if (reason is not null)
            {
                lastFailureHealth = ExternalResourceHealth.CapacityExceeded;
                lastFailureReason = reason;
                return new(false, null, reason, evicted);
            }

            reservedDiskBytes += request.DiskBytes;
            reservedMemoryBytes += request.MemoryBytes;
            reservedIdentityCounts[request.Identity] =
                reservedIdentityCounts.TryGetValue(request.Identity, out var identityCount)
                    ? identityCount + 1
                    : 1;
            return new(
                true,
                new ExternalResourceReservation(this, request),
                null,
                evicted);
        }
    }

    internal ExternalResourceLease? PromoteReservation(ExternalResourceReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0
                || !ReferenceEquals(reservation.Registry, this)
                || !reservation.TryPromote())
            {
                return null;
            }

            ReleaseReservationNoLock(reservation);
            if (entries.TryGetValue(reservation.Request.Identity, out var resident))
            {
                resident.LeaseCount++;
                resident.LastUsedUtc = ExternalResourceRegistrySupport.UtcNow(clock);
                return new ExternalResourceLease(this, reservation.Request.Identity);
            }

            entries.Add(
                reservation.Request.Identity,
                new ResourceEntry(
                    reservation.Request.DiskBytes,
                    reservation.Request.MemoryBytes,
                    ExternalResourceRegistrySupport.UtcNow(clock)));
            return new ExternalResourceLease(this, reservation.Request.Identity);
        }
    }

    internal void ReleaseReservation(ExternalResourceReservation reservation)
    {
        lock (gate)
        {
            ReleaseReservationNoLock(reservation);
        }
    }

    internal bool Remove(string identity)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(identity, out var entry) || entry.LeaseCount != 0)
            {
                return false;
            }

            return entries.Remove(identity);
        }
    }

    internal void EndOperation()
    {
        lock (gate)
        {
            if (activeOperations > 0) activeOperations--;
            if (Volatile.Read(ref disposed) != 0) return;
        }

        ExternalResourceRegistrySupport.ReleaseOperationSlot(operationSlots);
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

    private void EvictLeastRecentlyUsedNoLock(ExternalResourceRequest request, ICollection<string> evicted)
    {
        if (request.DiskBytes > options.MaxDiskBytes || request.MemoryBytes > options.MaxMemoryBytes)
        {
            return;
        }

        while (CapacityReasonNoLock(request) is not null)
        {
            var victim = entries
                .Where(pair => pair.Value.LeaseCount == 0)
                .OrderBy(pair => pair.Value.LastUsedUtc)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (victim.Key is null) return;
            entries.Remove(victim.Key);
            evicted.Add(victim.Key);
        }
    }

    private string? CapacityReasonNoLock(ExternalResourceRequest request) =>
        ExternalResourceRegistrySupport.CapacityReason(
            new ExternalResourceCapacityContext<ResourceEntry>
            {
                Entries = entries.Values,
                Options = options,
                ReservedDiskBytes = reservedDiskBytes,
                ReservedMemoryBytes = reservedMemoryBytes,
                ReservedResources = reservedIdentityCounts.Keys.Count(identity => !entries.ContainsKey(identity)),
                DiskSelector = entry => entry.DiskBytes,
                MemorySelector = entry => entry.MemoryBytes,
                RequestedResources = entries.ContainsKey(request.Identity)
                    || reservedIdentityCounts.ContainsKey(request.Identity)
                    ? 0
                    : 1,
            },
            request);

    private void ReleaseReservationNoLock(ExternalResourceReservation reservation)
    {
        reservedDiskBytes = Math.Max(0, reservedDiskBytes - reservation.Request.DiskBytes);
        reservedMemoryBytes = Math.Max(0, reservedMemoryBytes - reservation.Request.MemoryBytes);
        if (!reservedIdentityCounts.TryGetValue(reservation.Request.Identity, out var identityCount))
        {
            return;
        }

        if (identityCount <= 1)
        {
            reservedIdentityCounts.Remove(reservation.Request.Identity);
        }
        else
        {
            reservedIdentityCounts[reservation.Request.Identity] = identityCount - 1;
        }
    }

    private void EvictExpiredNoLock(DateTime now, ICollection<string> evicted)
    {
        var expired = entries
            .Where(pair => pair.Value.LeaseCount == 0 && now - pair.Value.LastUsedUtc > idleTtl)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var identity in expired)
        {
            entries.Remove(identity);
            evicted.Add(identity);
        }
    }

    private static void ValidateRequest(ExternalResourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Identity)) throw new ArgumentException(EmptyIdentityMessage, nameof(request));
        if (request.DiskBytes < 0) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.MemoryBytes < 0) throw new ArgumentOutOfRangeException(nameof(request));
    }

    private ExternalResourceHealthSnapshot CreateHealthNoLock(ExternalResourceHealth health) =>
        ExternalResourceRegistrySupport.CreateHealth(
            health,
            new ExternalResourceHealthContext<ResourceEntry>
            {
                LastFailureReason = lastFailureReason,
                Entries = entries.Values,
                Options = options,
                ActiveOperations = activeOperations,
                DiskSelector = entry => entry.DiskBytes,
                MemorySelector = entry => entry.MemoryBytes,
            });

    private sealed class ResourceEntry(long diskBytes, long memoryBytes, DateTime lastUsedUtc)
    {
        internal long DiskBytes { get; } = diskBytes;
        internal long MemoryBytes { get; } = memoryBytes;
        internal DateTime LastUsedUtc { get; set; } = lastUsedUtc;
        internal int LeaseCount { get; set; } = 1;
    }
}
