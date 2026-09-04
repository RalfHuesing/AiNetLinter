#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection;

internal interface IAssemblySourceResolver
{
    Task<AssemblySourceResolution> ResolveForRegistryAsync(
        string assemblyPath,
        CancellationToken cancellationToken);
}

internal interface IAssemblySourceSnapshotIdentityCache
{
    bool TryGetCachedSourceSnapshotIdentity(
        string assemblyPath,
        out string? snapshotIdentity);
}

internal interface IAssemblySourceSelectionResolver
{
    Task<AssemblySourceSelectionScope> ResolveAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}

internal interface IAssemblySourceProviderCoordinator : IAsyncDisposable
{
    Task<AssemblySourceProviderResultLease> LeaseProviderResultAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken);

    SourceSnapshotLease AcquireSnapshot(
        AssemblySourceProviderResultLease providerLease,
        ExternalSourceSnapshot snapshot);

    void RememberSnapshotIdentity(string assemblyPath, string snapshotIdentity);

    bool TryGetCachedSnapshotIdentity(string assemblyPath, out string? snapshotIdentity);

    void RememberNegativeResult(string assemblyPath, string fallbackReason, IReadOnlyList<ExternalSourceConfigurationDiagnostic>? diagnostics = null, TimeSpan? ttl = null);

    bool TryGetNegativeResult(string assemblyPath, out string? fallbackReason, out IReadOnlyList<ExternalSourceConfigurationDiagnostic>? diagnostics);
}

internal sealed record AssemblySourceResolution(
    AssemblySourceSelection? Selection,
    IDisposable? Lifetime,
    IReadOnlyList<ExternalSourceConfigurationDiagnostic> Diagnostics,
    AssemblySourceFallbackMetadata? Fallback = null,
    ExternalSourceSourceMode SourceMode = ExternalSourceSourceMode.SourcePreferred);
