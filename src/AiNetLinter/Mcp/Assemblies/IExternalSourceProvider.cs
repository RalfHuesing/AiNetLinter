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
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceSnapshot? sourceSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!isAvailable && sourceSnapshot is not null)
        {
            throw new ArgumentException(
                "Ein Snapshot darf nur mit einem verfügbaren Provider-Ergebnis transportiert werden.",
                nameof(sourceSnapshot));
        }

        IsAvailable = isAvailable;
        Diagnostics = diagnostics.ToImmutableArray();
        SourceSnapshot = sourceSnapshot;
    }

    internal bool IsAvailable { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal ExternalSourceSnapshot? SourceSnapshot { get; }
}
