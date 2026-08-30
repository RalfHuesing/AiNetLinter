#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal enum AssemblySourceSelectionStatus { Matched, NoMatch, Ambiguous, ProviderUnavailable, ConfigurationFailure }

internal sealed class AssemblySourceSelectionOrchestrator
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

        var resolution = new AssemblyReferenceResolver().Resolve(assemblyPath);
        var assemblyName = resolution.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(assemblyName)) return CreateScope();

        var mappings = configurationResult.Configuration!.Mappings
            .Where(mapping => mapping.Assemblies.Any(alias =>
                string.Equals(alias.Trim(), assemblyName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (mappings.Count != 1) return CreateScope();

        cancellationToken.ThrowIfCancellationRequested();
        var mapping = mappings[0];
        var providerResult = await provider.ResolveAsync(mapping, cancellationToken).ConfigureAwait(false);
        if (!providerResult.IsAvailable || providerResult.SourceSnapshot is null)
            return CreateScope(providerResult.FailureKind, providerResult.Diagnostics);

        SourceSnapshotLease lease;
        try
        {
            lease = registry.Acquire(providerResult.SourceSnapshot);
        }
        catch { providerResult.SourceSnapshot.Dispose(); throw; }

        try
        {
            var match = AssemblySourceMatchResolver.Resolve(lease, mapping, assemblyName);
            var selection = AssemblySourceSelection.Create(lease, match);
            return new AssemblySourceSelectionScope(
                selection,
                configurationResult,
                providerResult.Diagnostics,
                lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private AssemblySourceSelectionScope CreateScope(
        ExternalSourceProviderFailureKind providerFailureKind = ExternalSourceProviderFailureKind.None,
        IEnumerable<ExternalSourceConfigurationDiagnostic>? providerDiagnostics = null) =>
        new(
            null,
            configurationResult,
            providerDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>(),
            null,
            providerFailureKind);
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
        ExternalSourceProviderFailureKind providerFailureKind = ExternalSourceProviderFailureKind.None)
    {
        ArgumentNullException.ThrowIfNull(configurationResult);
        ArgumentNullException.ThrowIfNull(providerDiagnostics);

        Selection = selection;
        this.configurationResult = configurationResult;
        LoaderDiagnostics = configurationResult.Diagnostics;
        ProviderDiagnostics = providerDiagnostics.ToImmutableArray();
        ProviderFailureKind = providerFailureKind;
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
            : ProviderFailureKind is not ExternalSourceProviderFailureKind.None
            ? AssemblySourceSelectionStatus.ProviderUnavailable
            : AssemblySourceSelectionStatus.NoMatch;

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> LoaderDiagnostics { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> ProviderDiagnostics { get; }

    internal ExternalSourceProviderFailureKind ProviderFailureKind { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    private readonly ExternalSourceConfigurationLoadResult configurationResult;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Exchange(ref lease, null)?.Dispose();
    }
}
