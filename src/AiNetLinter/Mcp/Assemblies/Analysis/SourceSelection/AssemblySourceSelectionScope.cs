#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

namespace AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;

internal sealed record AssemblySourceSelectionScopeParameters(
    AssemblySourceSelection? Selection,
    AssemblySourceSelectionConfiguration Configuration,
    IEnumerable<ExternalSourceConfigurationDiagnostic> ProviderDiagnostics,
    SourceSnapshotLease? Lease,
    ExternalSourceRepositoryResultState? State = null,
    AssemblySourceFallbackMetadata? Fallback = null,
    IEnumerable<ExternalSourceConfigurationDiagnostic>? SourceDiagnostics = null);

internal sealed class AssemblySourceSelectionScope : IDisposable
{
    private SourceSnapshotLease? lease;
    private int disposed;

    internal AssemblySourceSelectionScope(AssemblySourceSelectionScopeParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.ProviderDiagnostics);
        ArgumentNullException.ThrowIfNull(parameters.Configuration);
        var state = parameters.State ?? ExternalSourceRepositoryResultState.Create();

        Selection = parameters.Selection;
        configuration = parameters.Configuration;
        LoaderDiagnostics = configuration.LoaderDiagnostics;
        ProviderDiagnostics = parameters.ProviderDiagnostics.ToImmutableArray();
        ProviderFailureKind = state.FailureKind;
        ProviderHealth = ExternalSourceRepositorySourcePolicy.ResolveHealth(
            Selection is not null,
            state.Health,
            state.LastGoodRevision);
        ProviderCheckoutTrust = state.CheckoutTrust
            ?? (Selection is not null ? ExternalSourceCheckoutTrust.Clean : ExternalSourceCheckoutTrust.Unverified);
        LastGoodRevision = ExternalSourceRepositorySourcePolicy.NormalizeLastGoodRevision(state.LastGoodRevision);
        Diagnostics = LoaderDiagnostics.AddRange(ProviderDiagnostics).Distinct().ToImmutableArray();
        Fallback = parameters.Fallback;
        SourceDiagnostics = (parameters.SourceDiagnostics ?? ProviderDiagnostics).Distinct().ToImmutableArray();
        lease = parameters.Lease;
    }

    internal AssemblySourceSelection? Selection { get; }

    internal AssemblySourceSelectionStatus Status =>
        Selection is not null ? (AssemblySourceSelectionStatus)Selection.MatchResult.State :
        Fallback?.Reason is AssemblySourceFallbackReasons.MappingAmbiguous
            ? AssemblySourceSelectionStatus.Ambiguous :
        !configuration.Succeeded
            ? AssemblySourceSelectionStatus.ConfigurationFailure :
        ProviderHealth is ExternalSourceRepositoryHealth.Degraded
            ? AssemblySourceSelectionStatus.ProviderDegraded :
        ProviderFailureKind is not ExternalSourceProviderFailureKind.None
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

    internal AssemblySourceFallbackMetadata? Fallback { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> SourceDiagnostics { get; }

    private readonly AssemblySourceSelectionConfiguration configuration;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Exchange(ref lease, null)?.Dispose();
    }
}
