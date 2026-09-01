#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;

internal sealed class AssemblySourceProviderCoordinator : IAssemblySourceProviderCoordinator
{
    private readonly IExternalSourceProvider provider;
    private readonly IAssemblySourceSelectionSnapshotRegistry registry;
    private readonly Func<Task>? afterCreationCompletedBeforeRemovalAsync;
    private readonly Lock creationGate = new();
    private readonly Dictionary<string, AssemblySourceProviderCreation> creations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> cachedSnapshotIdentities = new(StringComparer.OrdinalIgnoreCase);
    private Task? disposalTask;
    private int disposed;

    Task<AssemblySourceProviderResultLease> IAssemblySourceProviderCoordinator.LeaseProviderResultAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken) =>
        LeaseProviderResultAsync(mapping, cancellationToken);

    SourceSnapshotLease IAssemblySourceProviderCoordinator.AcquireSnapshot(
        AssemblySourceProviderResultLease providerLease,
        ExternalSourceSnapshot snapshot) =>
        AcquireSnapshot(providerLease, snapshot);

    void IAssemblySourceProviderCoordinator.RememberSnapshotIdentity(
        string assemblyPath,
        string snapshotIdentity) =>
        RememberSnapshotIdentity(assemblyPath, snapshotIdentity);

    bool IAssemblySourceProviderCoordinator.TryGetCachedSnapshotIdentity(
        string assemblyPath,
        out string? snapshotIdentity) =>
        TryGetCachedSnapshotIdentity(assemblyPath, out snapshotIdentity);

    internal AssemblySourceProviderCoordinator(
        IExternalSourceProvider provider,
        IAssemblySourceSelectionSnapshotRegistry registry,
        Func<Task>? afterCreationCompletedBeforeRemovalAsync = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registry);
        this.provider = provider;
        this.registry = registry;
        this.afterCreationCompletedBeforeRemovalAsync = afterCreationCompletedBeforeRemovalAsync;
    }

    internal async Task<AssemblySourceProviderResultLease> LeaseProviderResultAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var key = CreateCreationKey(mapping);
        AssemblySourceProviderCreation creation;
        lock (creationGate)
        {
            ThrowIfDisposed();
            if (!creations.TryGetValue(key, out creation!))
            {
                creation = new AssemblySourceProviderCreation();
                creations.Add(key, creation);
                creation.AddWaiter();
                creation.SetProducerTask(RunProviderCreationAsync(key, mapping, creation));
            }
            else
            {
                creation.AddWaiter();
            }
        }

        try
        {
            var result = await creation.Completion.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return new AssemblySourceProviderResultLease(creation, result);
        }
        catch
        {
            creation.ReleaseWaiter(accepted: false);
            throw;
        }
    }

    internal SourceSnapshotLease AcquireSnapshot(
        AssemblySourceProviderResultLease providerLease,
        ExternalSourceSnapshot snapshot)
    {
        try
        {
            var lease = registry.Acquire(snapshot);
            providerLease.AcceptSnapshot();
            return lease;
        }
        catch
        {
            ExternalSourceSnapshotDisposal.DisposeBestEffort(snapshot, "Snapshot nach Registry-Fehler");
            throw;
        }
    }

    internal void RememberSnapshotIdentity(string assemblyPath, string snapshotIdentity)
    {
        var canonicalPath = Path.GetFullPath(assemblyPath);
        lock (creationGate)
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                cachedSnapshotIdentities[canonicalPath] = snapshotIdentity;
            }
        }
    }

    internal bool TryGetCachedSnapshotIdentity(string assemblyPath, out string? snapshotIdentity)
    {
        var canonicalPath = Path.GetFullPath(assemblyPath);
        lock (creationGate)
        {
            return cachedSnapshotIdentities.TryGetValue(canonicalPath, out snapshotIdentity);
        }
    }

    public ValueTask DisposeAsync() => new(StartDispose());

    private Task StartDispose()
    {
        AssemblySourceProviderCreation[] pending;
        TaskCompletionSource<object?>? completion = null;
        lock (creationGate)
        {
            if (disposalTask is not null)
            {
                return disposalTask;
            }

            Interlocked.Exchange(ref disposed, 1);
            pending = creations.Values.ToArray();
            creations.Clear();
            cachedSnapshotIdentities.Clear();
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            disposalTask = completion.Task;
        }

        foreach (var creation in pending)
        {
            creation.Cancel();
        }

        _ = JoinProducerCreationsAsync(pending, completion);
        return completion.Task;
    }

    private static async Task JoinProducerCreationsAsync(
        IReadOnlyList<AssemblySourceProviderCreation> pending,
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await Task.WhenAll(pending.Select(creation => creation.ProducerTask)).ConfigureAwait(false);
            completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task RunProviderCreationAsync(
        string key,
        ExternalSourceMapping mapping,
        AssemblySourceProviderCreation creation)
    {
        try
        {
            using var operation = registry.BeginOperation(creation.CreationToken);
            var result = await provider.ResolveAsync(mapping, creation.CreationToken)
                .ConfigureAwait(false);
            if (!creation.TrySetResult(result))
            {
                creation.DisposeRejectedResult(result);
            }
        }
        catch (OperationCanceledException exception) when (
            exception.CancellationToken.IsCancellationRequested
            || creation.CreationToken.IsCancellationRequested)
        {
            creation.Completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            creation.Completion.TrySetException(exception);
        }
        catch (Exception exception)
        {
            creation.Completion.TrySetException(exception);
        }
        finally
        {
            creation.Complete();
            if (afterCreationCompletedBeforeRemovalAsync is not null)
            {
                await afterCreationCompletedBeforeRemovalAsync().ConfigureAwait(false);
            }

            lock (creationGate)
            {
                if (creations.TryGetValue(key, out var current)
                    && ReferenceEquals(current, creation))
                {
                    creations.Remove(key);
                }
            }
        }
    }

    private static string CreateCreationKey(ExternalSourceMapping mapping)
    {
        if (ExternalSourceRepositoryCacheKey.TryCreate(
                mapping.Url,
                mapping.SolutionPath,
                out var cacheKey))
        {
            return cacheKey!.StableValue;
        }

        return string.Concat(mapping.Url.Trim(), "|", mapping.SolutionPath.Trim());
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(AssemblySourceProviderCoordinator));
        }
    }
}
