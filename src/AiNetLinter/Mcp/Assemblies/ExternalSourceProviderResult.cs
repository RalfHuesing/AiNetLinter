#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

internal sealed record ExternalSourceProviderResult
{
    internal ExternalSourceProviderResult(
        bool isAvailable,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceSnapshot? sourceSnapshot = null,
        ExternalSourceRepositoryResultState? state = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        state ??= ExternalSourceRepositoryResultState.Create();
        var validatedState = ExternalSourceRepositorySourcePolicy.ValidateResultState(isAvailable, state);

        if (!isAvailable && sourceSnapshot is not null)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein Snapshot darf nur mit einem verfügbaren Provider-Ergebnis transportiert werden.",
                nameof(sourceSnapshot));
        }

        if (isAvailable && sourceSnapshot is null)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verifiziertes Provider-Ergebnis benötigt einen Source-Snapshot.",
                nameof(sourceSnapshot));
        }

        if (isAvailable && state.FailureKind is not ExternalSourceProviderFailureKind.None)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verfügbares Provider-Ergebnis darf keinen Fehlerzustand transportieren.",
                nameof(state));
        }

        IsAvailable = isAvailable;
        FailureKind = isAvailable || state.FailureKind is not ExternalSourceProviderFailureKind.None
            ? state.FailureKind
            : ExternalSourceProviderFailureKind.ProviderUnavailable;
        Health = validatedState.Health;
        LastGoodRevision = validatedState.LastGoodRevision;
        Diagnostics = diagnostics.ToImmutableArray();
        SourceSnapshot = sourceSnapshot;
    }

    internal bool IsAvailable { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ExternalSourceRepositoryHealth Health { get; }

    internal string? LastGoodRevision { get; }

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

        return new ExternalSourceProviderResult(
            isAvailable: false,
            acquisition.Diagnostics,
            state: ExternalSourceRepositoryResultState.Create(
                acquisition.FailureKind,
                acquisition.Health,
                acquisition.LastGoodRevision));
    }
}
