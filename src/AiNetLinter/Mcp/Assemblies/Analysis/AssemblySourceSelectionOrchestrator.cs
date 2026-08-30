#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal enum AssemblySourceSelectionStatus
{
    Matched,
    NoMatch,
    Ambiguous,
    ProviderUnavailable,
    ProviderDegraded,
    ConfigurationFailure,
}

internal sealed class AssemblySourceSelectionOrchestrator : IAssemblySourceResolver, IAssemblySourceSelectionResolver
{
    private readonly ExternalSourceConfigurationLoadResult configurationResult;
    private readonly IExternalSourceProvider provider;
    private readonly SourceSnapshotRegistry registry;
    private readonly Lock creationGate = new();
    private readonly Dictionary<string, SharedProviderCreation> creations = new(StringComparer.Ordinal);
    private int disposed;

    internal AssemblySourceSelectionOrchestrator(
        ExternalSourceConfigurationLoadResult configurationResult,
        IExternalSourceProvider provider,
        SourceSnapshotRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(configurationResult);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registry);

        this.configurationResult = configurationResult;
        this.provider = provider;
        this.registry = registry;
    }

    internal static AssemblySourceSelectionOrchestrator CreateFromSettings(
        string? settingsPath,
        IExternalSourceProvider provider,
        SourceSnapshotRegistry registry) =>
        new(ExternalSourceConfigurationLoader.Load(settingsPath), provider, registry);

    internal async Task<AssemblySourceSelectionScope> ResolveAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (!configurationResult.Succeeded) return CreateScope();

        var assemblyName = ResolveAssemblyName(assemblyPath);
        if (string.IsNullOrWhiteSpace(assemblyName)) return CreateScope();

        var mappings = FindMappings(assemblyName);
        if (mappings.Count != 1) return CreateScope();

        cancellationToken.ThrowIfCancellationRequested();
        using var providerLease = await LeaseProviderResultAsync(mappings[0], cancellationToken).ConfigureAwait(false);
        var providerResult = providerLease.Result;
        if (!providerResult.IsAvailable || providerResult.SourceSnapshot is null)
        {
            return CreateScope(
                providerResult.Diagnostics,
                providerResult.ToResultState());
        }

        if (!IsTrusted(providerResult))
        {
            return RejectUntrustedSnapshot(providerResult);
        }

        SourceSnapshotLease lease;
        try
        {
            lease = registry.Acquire(providerResult.SourceSnapshot);
            providerLease.AcceptSnapshot();
        }
        catch
        {
            DisposeSnapshotBestEffort(providerResult.SourceSnapshot, "Snapshot nach Registry-Fehler");
            throw;
        }

        try
        {
            var match = AssemblySourceMatchResolver.Resolve(lease, mappings[0], assemblyName);
            var selection = AssemblySourceSelection.Create(
                new AssemblySourceSelectionParameters(
                    lease,
                    match,
                    providerResult.Health,
                    providerResult.CheckoutTrust,
                    providerResult.IsAttested));
            return new AssemblySourceSelectionScope(
                selection,
                configurationResult,
                providerResult.Diagnostics,
                lease,
                providerResult.ToResultState());
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    async Task<AssemblySourceResolution> IAssemblySourceResolver.ResolveForRegistryAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
        return new(scope.Selection, scope, scope.Diagnostics);
    }

    Task<AssemblySourceSelectionScope> IAssemblySourceSelectionResolver.ResolveAsync(
        string assemblyPath,
        CancellationToken cancellationToken) =>
        ResolveAsync(assemblyPath, cancellationToken);

    internal void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        SharedProviderCreation[] pending;
        lock (creationGate)
        {
            pending = creations.Values.ToArray();
            creations.Clear();
        }

        foreach (var creation in pending)
        {
            creation.Cancel();
        }
    }

    private async Task<SharedProviderResultLease> LeaseProviderResultAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var key = CreateCreationKey(mapping);
        SharedProviderCreation creation;
        lock (creationGate)
        {
            ThrowIfDisposed();
            if (!creations.TryGetValue(key, out creation!))
            {
                creation = new SharedProviderCreation(cancellationToken);
                creations.Add(key, creation);
                creation.AddWaiter();
                _ = RunProviderCreationAsync(key, mapping, creation);
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
            return new SharedProviderResultLease(creation, result);
        }
        catch
        {
            creation.ReleaseWaiter(accepted: false);
            throw;
        }
    }

    private async Task RunProviderCreationAsync(
        string key,
        ExternalSourceMapping mapping,
        SharedProviderCreation creation)
    {
        try
        {
            using var operation = registry.BeginOperation(creation.CreationToken);
            var result = await provider.ResolveAsync(mapping, creation.CreationToken)
                .ConfigureAwait(false);
            if (!creation.TrySetResult(result))
            {
                DisposeSnapshotBestEffort(result.SourceSnapshot, "Provider-Creation nach Orchestrator-Dispose");
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
            lock (creationGate)
            {
                if (creations.TryGetValue(key, out var current)
                    && ReferenceEquals(current, creation))
                {
                    creations.Remove(key);
                }
            }

            creation.Complete();
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
            throw new ObjectDisposedException(nameof(AssemblySourceSelectionOrchestrator));
        }
    }

    private string? ResolveAssemblyName(string assemblyPath) =>
        new AssemblyReferenceResolver().Resolve(assemblyPath).Identity?.Name?.Trim();

    private IReadOnlyList<ExternalSourceMapping> FindMappings(string assemblyName) =>
        configurationResult.Configuration!.Mappings
            .Where(mapping => mapping.Assemblies.Any(alias =>
                string.Equals(alias.Trim(), assemblyName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private static bool IsTrusted(ExternalSourceProviderResult providerResult) =>
        providerResult.IsAttested
        && providerResult.Health is ExternalSourceRepositoryHealth.Verified
        && providerResult.CheckoutTrust is ExternalSourceCheckoutTrust.Clean;

    private AssemblySourceSelectionScope RejectUntrustedSnapshot(ExternalSourceProviderResult providerResult)
    {
        var diagnostics = providerResult.Diagnostics
            .Append(new ExternalSourceConfigurationDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified,
                "Der Source-Snapshot ist nicht als clean, verifiziert und attestiert ausgewiesen; die Assembly wird decompiliert.",
                "warning",
                "$repository"));
        return CreateScope(
            diagnostics,
            ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceRepositoryHealth.Unavailable,
                checkoutTrust: ExternalSourceCheckoutTrust.Unverified));
    }

    private AssemblySourceSelectionScope CreateScope(
        IEnumerable<ExternalSourceConfigurationDiagnostic>? providerDiagnostics = null,
        ExternalSourceRepositoryResultState? state = null) =>
        new(
            null,
            configurationResult,
            providerDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>(),
            null,
            state ?? ExternalSourceRepositoryResultState.Create());

    private static void DisposeSnapshotBestEffort(ExternalSourceSnapshot? snapshot, string reason)
    {
        try
        {
            snapshot?.Dispose();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "External-Source-Snapshot konnte nicht vollständig freigegeben werden: Grund={Reason}", reason);
        }
    }

    private sealed class SharedProviderCreation
    {
        internal SharedProviderCreation(CancellationToken creationToken) => CreationToken = creationToken;

        internal CancellationToken CreationToken { get; }

        internal readonly TaskCompletionSource<ExternalSourceProviderResult> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ExternalSourceProviderResult? completedResult;
        private int waiters;
        private int completed;
        private int snapshotAccepted;

        internal void AddWaiter() => Interlocked.Increment(ref waiters);

        internal bool TrySetResult(ExternalSourceProviderResult result)
        {
            if (!Completion.TrySetResult(result))
            {
                return false;
            }

            completedResult = result;
            return true;
        }

        internal void ReleaseWaiter(bool accepted)
        {
            if (accepted)
            {
                Interlocked.Exchange(ref snapshotAccepted, 1);
            }

            if (Interlocked.Decrement(ref waiters) == 0
                && Volatile.Read(ref completed) != 0
                && Volatile.Read(ref snapshotAccepted) == 0)
            {
                DisposeResultSnapshot();
            }
        }

        internal void Complete()
        {
            Interlocked.Exchange(ref completed, 1);
            if (Volatile.Read(ref waiters) == 0
                && Volatile.Read(ref snapshotAccepted) == 0)
            {
                DisposeResultSnapshot();
            }

        }

        internal void Cancel() => Completion.TrySetCanceled();

        private void DisposeResultSnapshot()
        {
            var result = Volatile.Read(ref completedResult);
            if (result is null)
            {
                return;
            }

            DisposeSnapshotBestEffort(
                result.SourceSnapshot,
                "Provider-Creation ohne Consumer-Lease");
        }
    }

    private sealed class SharedProviderResultLease : IDisposable
    {
        private readonly SharedProviderCreation creation;
        private int accepted;
        private int disposed;

        internal SharedProviderResultLease(
            SharedProviderCreation creation,
            ExternalSourceProviderResult result)
        {
            this.creation = creation;
            Result = result;
        }

        internal ExternalSourceProviderResult Result { get; }

        internal void AcceptSnapshot() => Interlocked.Exchange(ref accepted, 1);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                creation.ReleaseWaiter(Volatile.Read(ref accepted) != 0);
            }
        }
    }
}

internal sealed class AssemblySourceSelectionScope : IDisposable
{
    private SourceSnapshotLease? lease;
    private int disposed;

    internal AssemblySourceSelectionScope(
        AssemblySourceSelection? selection,
        ExternalSourceConfigurationLoadResult configurationResult,
        IEnumerable<ExternalSourceConfigurationDiagnostic> providerDiagnostics,
        SourceSnapshotLease? lease,
        ExternalSourceRepositoryResultState? state = null)
    {
        ArgumentNullException.ThrowIfNull(configurationResult);
        ArgumentNullException.ThrowIfNull(providerDiagnostics);
        state ??= ExternalSourceRepositoryResultState.Create();

        Selection = selection;
        this.configurationResult = configurationResult;
        LoaderDiagnostics = configurationResult.Diagnostics;
        ProviderDiagnostics = providerDiagnostics.ToImmutableArray();
        ProviderFailureKind = state.FailureKind;
        ProviderHealth = ExternalSourceRepositorySourcePolicy.ResolveHealth(
            selection is not null,
            state.Health,
            state.LastGoodRevision);
        ProviderCheckoutTrust = state.CheckoutTrust
            ?? (selection is not null
                ? ExternalSourceCheckoutTrust.Clean
                : ExternalSourceCheckoutTrust.Unverified);
        LastGoodRevision = ExternalSourceRepositorySourcePolicy.NormalizeLastGoodRevision(state.LastGoodRevision);
        Diagnostics = LoaderDiagnostics
            .AddRange(ProviderDiagnostics)
            .Distinct()
            .ToImmutableArray();
        this.lease = lease;
    }

    internal AssemblySourceSelection? Selection { get; }

    internal AssemblySourceSelectionStatus Status =>
        Selection is not null ? (AssemblySourceSelectionStatus)Selection.MatchResult.State :
        !configurationResult.Succeeded
            ? AssemblySourceSelectionStatus.ConfigurationFailure
            : ProviderHealth is ExternalSourceRepositoryHealth.Degraded
            ? AssemblySourceSelectionStatus.ProviderDegraded
            : ProviderFailureKind is not ExternalSourceProviderFailureKind.None
            ? AssemblySourceSelectionStatus.ProviderUnavailable
            : AssemblySourceSelectionStatus.NoMatch;

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> LoaderDiagnostics { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> ProviderDiagnostics { get; }

    internal ExternalSourceProviderFailureKind ProviderFailureKind { get; }

    internal ExternalSourceRepositoryHealth ProviderHealth { get; }

    internal ExternalSourceCheckoutTrust ProviderCheckoutTrust { get; }

    internal string? LastGoodRevision { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    private readonly ExternalSourceConfigurationLoadResult configurationResult;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Exchange(ref lease, null)?.Dispose();
    }
}
