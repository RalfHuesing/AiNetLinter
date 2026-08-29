#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal interface IGiteaRepositoryTransport
{
    ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken = default);

    ValueTask<ExternalSourceRepositoryTransportResult> FetchDefaultBranchAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

internal sealed record ExternalSourceRepositoryTransportResult
{
    internal ExternalSourceRepositoryTransportResult(
        bool isAvailable,
        string? loadedRevision,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceProviderFailureKind failureKind = ExternalSourceProviderFailureKind.None)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        if (isAvailable && string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                "Ein verfügbares Akquisitionsergebnis benötigt eine geladene Revision.",
                nameof(loadedRevision));
        }

        if (!isAvailable && !string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                "Ein nicht verfügbares Akquisitionsergebnis darf keine Revision tragen.",
                nameof(loadedRevision));
        }

        if (isAvailable && failureKind is not ExternalSourceProviderFailureKind.None)
        {
            throw new ArgumentException(
                "Ein verfügbares Akquisitionsergebnis darf keinen Fehlerzustand tragen.",
                nameof(failureKind));
        }

        IsAvailable = isAvailable;
        LoadedRevision = string.IsNullOrWhiteSpace(loadedRevision) ? null : loadedRevision.Trim();
        FailureKind = isAvailable || failureKind is not ExternalSourceProviderFailureKind.None
            ? failureKind
            : ExternalSourceProviderFailureKind.ProviderUnavailable;
        Diagnostics = ExternalSourceRepositoryFailurePolicy.ProjectTransportDiagnostics(
            diagnostics,
            isAvailable,
            FailureKind);
    }

    internal bool IsAvailable { get; }

    internal string? LoadedRevision { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal static ExternalSourceRepositoryTransportResult Success(string revision) =>
        new(
            isAvailable: true,
            loadedRevision: revision,
            diagnostics: Array.Empty<ExternalSourceConfigurationDiagnostic>());
}
