#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
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
        var providerResult = await provider.ResolveAsync(mappings[0], cancellationToken).ConfigureAwait(false);
        if (!providerResult.IsAvailable || providerResult.SourceSnapshot is null)
        {
            DisposeSnapshotBestEffort(providerResult.SourceSnapshot, "nicht verfügbaren Provider-Snapshot");
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
        DisposeSnapshotBestEffort(providerResult.SourceSnapshot, "unverifizierten Source-Snapshot");
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
