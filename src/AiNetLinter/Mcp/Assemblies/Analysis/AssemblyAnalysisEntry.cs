#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisEntryCreateParameters(
    string CanonicalPath,
    Microsoft.CodeAnalysis.Solution Solution,
    AssemblyContext Context,
    IDisposable? Lifetime,
    ExternalResourceLease? ResourceLease = null,
    AssemblyReferenceLeaseFactory? ReferenceLeaseFactory = null,
    TimeProvider? Clock = null,
    Action<AssemblyAnalysisEntry>? OnReferenceLeaseReleased = null);

internal sealed record AssemblyAnalysisEntryResources(
    IDisposable? Lifetime,
    ExternalResourceLease? ResourceLease,
    Action<AssemblyAnalysisEntry>? OnReferenceLeaseReleased,
    AssemblyReferenceLeaseFactory? ReferenceLeaseFactory);

internal sealed class AssemblyAnalysisEntry : IAsyncDisposable, IAssemblyAnalysisEvictionEntry
{
    private readonly object gate = new();
    private readonly IDisposable? lifetime;
    private readonly ExternalResourceLease? resourceLease;
    private readonly Action<AssemblyAnalysisEntry>? onReferenceLeaseReleased;
    private readonly IAsyncDisposable stateLifetime;
    private TimeProvider clock = TimeProvider.System;
    private AssemblyReferenceLeaseFactory? referenceLeaseFactory;
    private readonly TaskCompletionSource<object?> leaseDrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTime lastUsedUtc = DateTime.UtcNow;
    private Task? disposeTask;
    private int leaseCount;
    private bool closing;

    internal AssemblyAnalysisEntry(
        string canonicalPath,
        ISolutionStateProvider state,
        IAsyncDisposable stateLifetime,
        AssemblyContext context,
        AssemblyAnalysisEntryResources resources)
    {
        CanonicalPath = canonicalPath;
        State = state;
        this.stateLifetime = stateLifetime;
        Context = context;
        lifetime = resources.Lifetime;
        resourceLease = resources.ResourceLease;
        onReferenceLeaseReleased = resources.OnReferenceLeaseReleased;
        referenceLeaseFactory = resources.ReferenceLeaseFactory;
        lastUsedUtc = UtcNow;
    }

    internal string CanonicalPath { get; }
    internal ISolutionStateProvider State { get; }
    internal AssemblyContext Context { get; }
    internal string ContentHash => Context.Origin.ContentHash;

    string IAssemblyAnalysisEvictionEntry.CanonicalPath => CanonicalPath;
    DateTime IAssemblyAnalysisEvictionEntry.LastUsedUtc => LastUsedUtc;
    bool IAssemblyAnalysisEvictionEntry.IsRetiring => IsRetiring;
    bool IAssemblyAnalysisEvictionEntry.IsIdleForCapacity() => IsIdleForCapacity();
    bool IAssemblyAnalysisEvictionEntry.IsIdle(DateTime now, TimeSpan idleTtl) => IsIdle(now, idleTtl);

    internal bool Matches(
        AssemblyFingerprint fingerprint,
        string? sourceSnapshotIdentity = null,
        bool compareSourceSnapshotIdentity = false) =>
        string.Equals(ContentHash, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase)
        && (!compareSourceSnapshotIdentity
            || string.Equals(
                Context.Origin.SourceSnapshotIdentity?.StableValue,
                sourceSnapshotIdentity,
                StringComparison.Ordinal));

    internal bool TryAcquireLease(out AssemblyAnalysisLease? lease) =>
        TryAcquireLease(referenceLeaseFactory, out lease);

    internal bool TryAcquireLease(
        AssemblyReferenceLeaseFactory? referenceLeaseFactory,
        out AssemblyAnalysisLease? lease)
    {
        lock (gate)
        {
            if (closing)
            {
                lease = null;
                return false;
            }

            leaseCount++;
            lastUsedUtc = UtcNow;
            lease = new(
                this,
                CanonicalPath,
                State,
                Context,
                new(referenceLeaseFactory, onReferenceLeaseReleased));
            return true;
        }
    }

    internal bool TryBeginRetirement()
    {
        lock (gate)
        {
            if (closing || leaseCount != 0) return false;
            closing = true;
            return true;
        }
    }

    internal bool IsRetiring
    {
        get
        {
            lock (gate) return closing;
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?>? completion = null;
        Task? drain = null;
        lock (gate)
        {
            if (disposeTask is null)
            {
                closing = true;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                disposeTask = completion.Task;
                drain = leaseCount == 0 ? Task.CompletedTask : leaseDrain.Task;
            }
        }

        if (completion is not null)
        {
            _ = DisposeAfterDrainAsync(drain!, completion);
        }

        return new(disposeTask!);
    }

    private async Task DisposeAfterDrainAsync(
        Task drain,
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await drain.ConfigureAwait(false);
            var failures = new List<Exception>();
            try
            {
                await stateLifetime.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                resourceLease?.DisposeAndRemove();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                if (lifetime is IAsyncDisposable asyncLifetime)
                {
                    await asyncLifetime.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    lifetime?.Dispose();
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count > 0)
            {
                completion.TrySetException(failures.Count == 1 ? failures[0] : new AggregateException(failures));
            }
            else
            {
                completion.TrySetResult(null);
            }
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    internal void ReleaseLease()
    {
        lock (gate)
        {
            if (leaseCount == 0) return;
            leaseCount--;
            if (closing && leaseCount == 0)
            {
                leaseDrain.TrySetResult(null);
            }
        }
    }

    internal void SetClock(TimeProvider? value)
    {
        lock (gate)
        {
            clock = value ?? TimeProvider.System;
            lastUsedUtc = UtcNow;
        }
    }

    internal bool IsIdle(DateTime now, TimeSpan idleTtl)
    {
        lock (gate)
        {
            return IsIdleNoLock(now, idleTtl, ignoreTtl: false);
        }
    }

    internal bool IsIdleForCapacity()
    {
        lock (gate)
        {
            return !closing && leaseCount == 0;
        }
    }

    internal DateTime LastUsedUtc
    {
        get
        {
            lock (gate) return lastUsedUtc;
        }
    }

    private bool IsIdleNoLock(DateTime now, TimeSpan idleTtl, bool ignoreTtl) =>
        !closing
        && leaseCount == 0
        && (ignoreTtl || now - lastUsedUtc > idleTtl);

    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

}
