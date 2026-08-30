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
        ExternalSourceCheckoutTrust? checkoutTrust = null,
        ExternalSourceRepositoryResultState? state = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        state ??= ExternalSourceRepositoryResultState.Create();
        var validatedState = ExternalSourceRepositorySourcePolicy.ValidateResultState(isAvailable, state);

        if (isAvailable && string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verfügbares Akquisitionsergebnis benötigt eine geladene Revision.",
                nameof(loadedRevision));
        }

        if (isAvailable
            && loadedRevision is not null
            && !ExternalSourceRepositoryCacheKey.IsSafeRevision(loadedRevision.Trim()))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verfügbares Akquisitionsergebnis benötigt eine sichere Revision.",
                nameof(loadedRevision));
        }

        if (!isAvailable && !string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein nicht verfügbares Akquisitionsergebnis darf keine Revision tragen.",
                nameof(loadedRevision));
        }

        if (isAvailable && state.FailureKind is not ExternalSourceProviderFailureKind.None)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verfügbares Akquisitionsergebnis darf keinen Fehlerzustand tragen.",
                nameof(state));
        }

        IsAvailable = isAvailable;
        LoadedRevision = string.IsNullOrWhiteSpace(loadedRevision) ? null : loadedRevision.Trim();
        FailureKind = isAvailable || state.FailureKind is not ExternalSourceProviderFailureKind.None
            ? state.FailureKind
            : ExternalSourceProviderFailureKind.ProviderUnavailable;
        CheckoutTrust = isAvailable
            ? checkoutTrust ?? ExternalSourceCheckoutTrust.Clean
            : checkoutTrust ?? ExternalSourceCheckoutTrust.Unverified;
        Health = validatedState.Health;
        LastGoodRevision = validatedState.LastGoodRevision;
        if (Health is ExternalSourceRepositoryHealth.Verified
            && CheckoutTrust is not ExternalSourceCheckoutTrust.Clean)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verifiziertes Transportergebnis benötigt einen cleanen Checkout.",
                nameof(checkoutTrust));
        }
        Diagnostics = isAvailable
            ? diagnostics.ToImmutableArray()
            : ExternalSourceRepositorySourcePolicy.ProjectFailureDiagnostics(
                diagnostics,
                Health,
                FailureKind);
    }

    internal bool IsAvailable { get; }

    internal string? LoadedRevision { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }

    internal ExternalSourceRepositoryHealth Health { get; }

    internal string? LastGoodRevision { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal static ExternalSourceRepositoryTransportResult Success(string revision) =>
        new(
            isAvailable: true,
            loadedRevision: revision,
            diagnostics: Array.Empty<ExternalSourceConfigurationDiagnostic>());
}
