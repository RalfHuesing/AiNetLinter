#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;

internal enum AssemblySourceSelectionStatus
{
    Matched,
    NoMatch,
    Ambiguous,
    ProviderUnavailable,
    ProviderDegraded,
    ConfigurationFailure,
}

internal static class AssemblySourceFallbackReasons
{
    internal const string ConfigurationInvalid = "configuration-invalid";
    internal const string AssemblyMetadataUnavailable = "assembly-metadata-unavailable";
    internal const string MappingNotFound = "mapping-not-found";
    internal const string MappingAmbiguous = "mapping-ambiguous";
    internal const string SourceProjectNotFound = "source-project-not-found";
    internal const string SourceProjectAmbiguous = "source-project-ambiguous";
    internal const string ProviderUnavailable = "provider-unavailable";
    internal const string ProviderDegraded = "provider-degraded";
    internal const string SnapshotUntrusted = "snapshot-untrusted";
    internal const string WorkspaceFailure = "workspace-failure";
}

internal sealed record AssemblySourceSelectionConfiguration(
    bool Succeeded,
    ImmutableArray<ExternalSourceMapping> Mappings,
    ImmutableArray<ExternalSourceConfigurationDiagnostic> LoaderDiagnostics);

internal sealed class AssemblySourceSelectionOrchestrator :
    IAssemblySourceResolver,
    IAssemblySourceSnapshotIdentityCache,
    IAssemblySourceSelectionResolver,
    IAsyncDisposable
{
    private readonly AssemblySourceSelectionConfiguration configuration;
    private readonly IReadOnlyList<ExternalSourceMapping> configuredMappings;
    private readonly IAssemblySourceProviderCoordinator providerCoordinator;

    internal AssemblySourceSelectionOrchestrator(
        AssemblySourceSelectionConfiguration configuration,
        IAssemblySourceProviderCoordinator providerCoordinator)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(providerCoordinator);

        this.configuration = configuration;
        configuredMappings = configuration.Mappings;
        this.providerCoordinator = providerCoordinator;
    }

    internal async Task<AssemblySourceSelectionScope> ResolveAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (!configuration.Succeeded)
        {
            return CreateScope(fallbackReason: AssemblySourceFallbackReasons.ConfigurationInvalid);
        }

        if (providerCoordinator.TryGetNegativeResult(assemblyPath, out var cachedNegativeFallback, out var cachedDiagnostics))
        {
            return CreateScope(cachedDiagnostics ?? [], fallbackReason: cachedNegativeFallback);
        }

        var assemblyName = ResolveAssemblyName(assemblyPath);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return CreateMetadataUnavailableScope();
        }

        var mappingResolution = ResolveMapping(assemblyName);
        if (mappingResolution.Scope is not null)
        {
            if (mappingResolution.Scope.Fallback?.Reason is not null)
            {
                providerCoordinator.RememberNegativeResult(assemblyPath, mappingResolution.Scope.Fallback.Reason, mappingResolution.Scope.Diagnostics);
            }

            return mappingResolution.Scope;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await ResolveMappedAssemblyAsync(
                    assemblyPath,
                    assemblyName,
                    mappingResolution.Mapping!,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateScope(
                [new(
                    ExternalSourceConfigurationDiagnosticCodes.WorkspaceDiagnostic,
                    "Die Source-Auswahl konnte wegen eines Provider- oder Workspace-Fehlers nicht verwendet werden.",
                    "error",
                    "$source")],
                ExternalSourceRepositoryResultState.Create(
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    ExternalSourceRepositoryHealth.Unavailable,
                    checkoutTrust: ExternalSourceCheckoutTrust.Unverified),
                AssemblySourceFallbackReasons.ProviderUnavailable);
        }
    }

    private async Task<AssemblySourceSelectionScope> ResolveMappedAssemblyAsync(
        string assemblyPath,
        string assemblyName,
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken)
    {
        using var providerLease = await providerCoordinator
            .LeaseProviderResultAsync(mapping, cancellationToken).ConfigureAwait(false);
        var providerResult = providerLease.Result;
        if (!providerResult.IsAvailable || providerResult.SourceSnapshot is null)
        {
            var fallbackReason = providerResult.Health is ExternalSourceRepositoryHealth.Degraded
                ? AssemblySourceFallbackReasons.ProviderDegraded
                : AssemblySourceFallbackReasons.ProviderUnavailable;
            providerCoordinator.RememberNegativeResult(assemblyPath, fallbackReason, providerResult.Diagnostics);
            return CreateScope(
                providerResult.Diagnostics,
                providerResult.ToResultState(),
                fallbackReason);
        }

        if (!IsTrusted(providerResult)) return RejectUntrustedSnapshot(providerResult);
        var lease = providerCoordinator.AcquireSnapshot(providerLease, providerResult.SourceSnapshot);
        return CreateSelectionScope(assemblyPath, assemblyName, mapping, providerResult, lease);
    }

    private AssemblySourceSelectionScope CreateSelectionScope(
        string assemblyPath,
        string assemblyName,
        ExternalSourceMapping mapping,
        ExternalSourceProviderResult providerResult,
        SourceSnapshotLease lease)
    {
        try
        {
            var match = AssemblySourceMatchResolver.Resolve(lease, mapping, assemblyName);
            var selection = AssemblySourceSelection.Create(
                new AssemblySourceSelectionParameters(
                    lease,
                    match,
                    providerResult.Health,
                    providerResult.CheckoutTrust,
                    providerResult.IsAttested));
            var matchDiagnostics = CreateMatchDiagnostics(match.State);
            var scope = new AssemblySourceSelectionScope(new(
                selection,
                configuration,
                providerResult.Diagnostics.Concat(lease.Snapshot.Diagnostics).Concat(matchDiagnostics),
                lease,
                providerResult.ToResultState(),
                CreateFallbackMetadata(GetMatchFallbackReason(match.State), lease.Snapshot.Diagnostics),
                lease.Snapshot.Diagnostics));
            if (selection is not null)
            {
                providerCoordinator.RememberSnapshotIdentity(
                    assemblyPath,
                    selection.SourceLease.Snapshot.Identity.StableValue);
            }
            else
            {
                var matchFallback = GetMatchFallbackReason(match.State);
                if (matchFallback is not null)
                {
                    providerCoordinator.RememberNegativeResult(assemblyPath, matchFallback, scope.Diagnostics);
                }
            }

            return scope;
        }
        catch (Exception)
        {
            lease.Dispose();
            return CreateScope(
                [new(
                    ExternalSourceConfigurationDiagnosticCodes.WorkspaceDiagnostic,
                    "Der Source-Snapshot konnte nicht in eine nutzbare Source-Selection überführt werden.",
                    "error",
                    "$workspace")],
                ExternalSourceRepositoryResultState.Create(
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    ExternalSourceRepositoryHealth.Unavailable,
                    checkoutTrust: ExternalSourceCheckoutTrust.Unverified),
                AssemblySourceFallbackReasons.WorkspaceFailure);
        }
    }

    async Task<AssemblySourceResolution> IAssemblySourceResolver.ResolveForRegistryAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
        return new(scope.Selection, scope, scope.Diagnostics, scope.Fallback);
    }

    Task<AssemblySourceSelectionScope> IAssemblySourceSelectionResolver.ResolveAsync(
        string assemblyPath,
        CancellationToken cancellationToken) =>
        ResolveAsync(assemblyPath, cancellationToken);

    bool IAssemblySourceSnapshotIdentityCache.TryGetCachedSourceSnapshotIdentity(
        string assemblyPath,
        out string? snapshotIdentity)
        => providerCoordinator.TryGetCachedSnapshotIdentity(assemblyPath, out snapshotIdentity);

    public ValueTask DisposeAsync() => providerCoordinator.DisposeAsync();

    private string? ResolveAssemblyName(string assemblyPath) =>
        new AssemblyReferenceResolver().Resolve(assemblyPath).Identity?.Name?.Trim();

    private AssemblySourceMappingResolution ResolveMapping(string assemblyName)
    {
        var mappings = FindMappings(assemblyName);
        if (mappings.Count == 1) return new(mappings[0], null);

        var isAmbiguous = mappings.Count > 1;
        var code = isAmbiguous
            ? ExternalSourceConfigurationDiagnosticCodes.AssemblyMappingAmbiguous
            : ExternalSourceConfigurationDiagnosticCodes.AssemblyMappingNotFound;
        var message = isAmbiguous
            ? $"Für Assembly '{assemblyName}' sind mehrere Source-Mappings konfiguriert."
            : $"Für Assembly '{assemblyName}' ist kein Source-Mapping konfiguriert.";
        var reason = isAmbiguous
            ? AssemblySourceFallbackReasons.MappingAmbiguous
            : AssemblySourceFallbackReasons.MappingNotFound;
        return new(null, CreateScope([new(code, message, "warning", "$configuration")], fallbackReason: reason));
    }

    private AssemblySourceSelectionScope CreateMetadataUnavailableScope() =>
        CreateScope(
            [new(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid,
                "Die Assembly-Metadaten konnten für die Source-Zuordnung nicht gelesen werden.",
                "warning",
                "$assembly")],
            fallbackReason: AssemblySourceFallbackReasons.AssemblyMetadataUnavailable);

    private static ExternalSourceConfigurationDiagnostic[] CreateMatchDiagnostics(ExternalSourceMatchState state) =>
        state switch
        {
            ExternalSourceMatchState.NoMatch => [new(
                ExternalSourceConfigurationDiagnosticCodes.SourceProjectNotFound,
                "Im Source-Snapshot wurde kein passendes Projekt gefunden.", "warning", "$solution")],
            ExternalSourceMatchState.Ambiguous => [new(
                ExternalSourceConfigurationDiagnosticCodes.SourceProjectAmbiguous,
                "Im Source-Snapshot wurden mehrere passende Projekte gefunden.", "warning", "$solution")],
            _ => [],
        };

    private static string? GetMatchFallbackReason(ExternalSourceMatchState state) =>
        state switch
        {
            ExternalSourceMatchState.NoMatch => AssemblySourceFallbackReasons.SourceProjectNotFound,
            ExternalSourceMatchState.Ambiguous => AssemblySourceFallbackReasons.SourceProjectAmbiguous,
            _ => null,
        };

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
                checkoutTrust: ExternalSourceCheckoutTrust.Unverified),
            AssemblySourceFallbackReasons.SnapshotUntrusted);
    }

    private AssemblySourceSelectionScope CreateScope(
        IEnumerable<ExternalSourceConfigurationDiagnostic>? providerDiagnostics = null,
        ExternalSourceRepositoryResultState? state = null,
        string? fallbackReason = null) =>
        new(new(
            null,
            configuration,
            providerDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>(),
            null,
            state ?? ExternalSourceRepositoryResultState.Create(),
            CreateFallbackMetadata(fallbackReason, providerDiagnostics),
            providerDiagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>()));

    private static AssemblySourceFallbackMetadata? CreateFallbackMetadata(
        string? reason,
        IEnumerable<ExternalSourceConfigurationDiagnostic>? diagnostics)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        return new(
            reason,
            (diagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>())
                .Distinct()
                .Take(20)
                .ToArray());
    }

    private sealed record AssemblySourceMappingResolution(
        ExternalSourceMapping? Mapping,
        AssemblySourceSelectionScope? Scope);
}
