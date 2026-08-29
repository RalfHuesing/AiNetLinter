#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal enum ExternalSourceProviderFailureKind
{
    None,
    ProviderUnavailable,
    AuthenticationRequired,
    AccessDenied,
    RepositoryNotFound,
    NetworkUnavailable,
    Timeout,
    InvalidResponse,
}

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
        ExternalSourceSnapshot? sourceSnapshot = null,
        ExternalSourceProviderFailureKind failureKind = ExternalSourceProviderFailureKind.None)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        if (!isAvailable && sourceSnapshot is not null)
        {
            throw new ArgumentException(
                "Ein Snapshot darf nur mit einem verfügbaren Provider-Ergebnis transportiert werden.",
                nameof(sourceSnapshot));
        }

        if (isAvailable && failureKind is not ExternalSourceProviderFailureKind.None)
        {
            throw new ArgumentException(
                "Ein verfügbares Provider-Ergebnis darf keinen Fehlerzustand transportieren.",
                nameof(failureKind));
        }

        IsAvailable = isAvailable;
        FailureKind = isAvailable || failureKind is not ExternalSourceProviderFailureKind.None
            ? failureKind
            : ExternalSourceProviderFailureKind.ProviderUnavailable;
        Diagnostics = diagnostics.ToImmutableArray();
        SourceSnapshot = sourceSnapshot;
    }

    internal bool IsAvailable { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal ExternalSourceSnapshot? SourceSnapshot { get; }
}

internal static class ExternalSourceProviderFailureProjection
{
    internal static ExternalSourceProviderResult FromUnavailableAcquisition(
        ExternalSourceRepositoryAcquisitionResult acquisition)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        if (acquisition.IsAvailable)
        {
            throw new ArgumentException(nameof(acquisition));
        }

        var diagnostics = ExternalSourceRepositoryFailurePolicy.ProjectTransportDiagnostics(
            acquisition.Diagnostics,
            isAvailable: false,
            acquisition.FailureKind);
        return new ExternalSourceProviderResult(
            isAvailable: false,
            diagnostics,
            failureKind: acquisition.FailureKind);
    }
}
