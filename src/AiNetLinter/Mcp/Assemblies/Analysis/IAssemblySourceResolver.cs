#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

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

internal sealed record AssemblySourceResolution(
    AssemblySourceSelection? Selection,
    IDisposable? Lifetime,
    IReadOnlyList<ExternalSourceConfigurationDiagnostic> Diagnostics,
    AssemblySourceFallbackMetadata? Fallback = null);
