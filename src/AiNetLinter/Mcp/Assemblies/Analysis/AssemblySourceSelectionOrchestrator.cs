#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

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

internal sealed record AssemblySourceSelectionConfiguration(
    bool Succeeded,
    ImmutableArray<ExternalSourceConfigurationDiagnostic> LoaderDiagnostics);

internal sealed class AssemblySourceSelectionOrchestrator :
    IAssemblySourceResolver,
    IAssemblySourceSelectionResolver,
    IAsyncDisposable
{
    private readonly AssemblySourceSelectionConfiguration configuration;
    private readonly IReadOnlyList<ExternalSourceMapping> configuredMappings;
    private readonly IExternalSourceProvider provider;
    private readonly IAssemblySourceSelectionSnapshotRegistry registry;
    private readonly Lock creationGate = new();
    private readonly Dictionary<string, AssemblySourceProviderCreation> creations = new(StringComparer.Ordinal);
    private Task? disposalTask;
    private int disposed;

    internal AssemblySourceSelectionOrchestrator(
        ExternalSourceConfigurationLoadResult configurationResult,
        IExternalSourceProvider provider,
        IAssemblySourceSelectionSnapshotRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(configurationResult);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registry);

        configuration = new(configurationResult.Succeeded, configurationResult.Diagnostics);
        configuredMappings = configurationResult.Configuration?.Mappings
            ?? ImmutableArray<ExternalSourceMapping>.Empty;
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
        if (!configuration.Succeeded) return CreateScope();

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
            ExternalSourceSnapshotDisposal.DisposeBestEffort(
                providerResult.SourceSnapshot,
                "Snapshot nach Registry-Fehler");
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
                configuration,
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

    private async Task<AssemblySourceProviderResultLease> LeaseProviderResultAsync(
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
        configuredMappings
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
            configuration,
            providerDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>(),
            null,
            state ?? ExternalSourceRepositoryResultState.Create());

}

internal sealed class AssemblySourceSelectionScope : IDisposable
{
    private SourceSnapshotLease? lease;
    private int disposed;

    internal AssemblySourceSelectionScope(
        AssemblySourceSelection? selection,
        AssemblySourceSelectionConfiguration configuration,
        IEnumerable<ExternalSourceConfigurationDiagnostic> providerDiagnostics,
        SourceSnapshotLease? lease,
        ExternalSourceRepositoryResultState? state = null)
    {
        ArgumentNullException.ThrowIfNull(providerDiagnostics);
        ArgumentNullException.ThrowIfNull(configuration);
        state ??= ExternalSourceRepositoryResultState.Create();

        Selection = selection;
        this.configuration = configuration;
        LoaderDiagnostics = configuration.LoaderDiagnostics;
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
        !configuration.Succeeded
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

    private readonly AssemblySourceSelectionConfiguration configuration;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Exchange(ref lease, null)?.Dispose();
    }
}
