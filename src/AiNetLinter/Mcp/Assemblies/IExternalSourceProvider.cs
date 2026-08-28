#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal interface IExternalSourceProvider
{
    ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default);
}

internal sealed record ExternalSourceProviderResult
{
    internal ExternalSourceProviderResult(
        bool isAvailable,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        IsAvailable = isAvailable;
        Diagnostics = diagnostics.ToImmutableArray();
    }

    internal bool IsAvailable { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }
}
