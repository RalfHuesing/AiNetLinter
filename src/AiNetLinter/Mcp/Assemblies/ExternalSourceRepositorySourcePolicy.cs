#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed record ExternalSourceRepositoryFailureAfterCleanupParameters(
    ExternalSourceCheckoutOwnership Ownership,
    ExternalSourceProviderFailureKind FailureKind,
    IEnumerable<ExternalSourceConfigurationDiagnostic> Diagnostics,
    string? LastGoodRevision,
    ExternalSourceCheckoutTrust CheckoutTrust);

internal static class ExternalSourceRepositorySourcePolicy
{
    internal static ExternalSourceCheckoutTrust NormalizeFailureTrust(
        ExternalSourceCheckoutTrust checkoutTrust) =>
        checkoutTrust is ExternalSourceCheckoutTrust.Dirty
            ? checkoutTrust
            : ExternalSourceCheckoutTrust.Unverified;

    internal static string? NormalizeLastGoodRevision(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
        {
            return null;
        }

        var normalized = revision.Trim();
        if (!ExternalSourceRepositoryCacheKey.IsSafeRevision(normalized))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Der Last-good-Commit muss eine sichere Revision sein.",
                nameof(revision));
        }

        return normalized;
    }

    internal static ExternalSourceRepositoryHealth ResolveHealth(
        bool isAvailable,
        ExternalSourceRepositoryHealth? requestedHealth,
        string? lastGoodRevision)
    {
        var normalizedLastGood = NormalizeLastGoodRevision(lastGoodRevision);
        var health = requestedHealth ?? ResolveDefaultHealth(isAvailable, normalizedLastGood);
        ValidateHealthValue(health, requestedHealth);
        ValidateAvailability(isAvailable, health, requestedHealth);
        ValidateLastGood(health, normalizedLastGood, lastGoodRevision);
        return health;
    }

    internal static (
        ExternalSourceRepositoryHealth Health,
        string? LastGoodRevision,
        ExternalSourceCheckoutTrust CheckoutTrust) ValidateResultState(
        bool isAvailable,
        ExternalSourceRepositoryResultState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(state.FailureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        var lastGoodRevision = NormalizeLastGoodRevision(state.LastGoodRevision);
        var checkoutTrust = state.CheckoutTrust
            ?? (isAvailable
                ? ExternalSourceCheckoutTrust.Clean
                : ExternalSourceCheckoutTrust.Unverified);
        ValidateCheckoutTrust(isAvailable, checkoutTrust);
        return (
            ResolveHealth(isAvailable, state.Health, lastGoodRevision),
            lastGoodRevision,
            checkoutTrust);
    }

    internal static bool IsVerifiedTransport(
        ExternalSourceRepositoryTransportResult result) =>
        result.IsAvailable
        && result.Health is ExternalSourceRepositoryHealth.Verified
        && result.CheckoutTrust is ExternalSourceCheckoutTrust.Clean
        && result.CheckoutAttestation is not null
        && result.LoadedRevision is not null
        && ExternalSourceRepositoryCacheKey.IsSafeRevision(result.LoadedRevision);

    internal static ImmutableArray<ExternalSourceConfigurationDiagnostic> ProjectFailureDiagnostics(
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceRepositoryHealth health,
        ExternalSourceProviderFailureKind failureKind)
    {
        var projected = ExternalSourceRepositoryFailurePolicy.ProjectTransportDiagnostics(
                diagnostics,
                isAvailable: false,
                failureKind)
            .ToList();
        if (health is ExternalSourceRepositoryHealth.Degraded)
        {
            projected.RemoveAll(diagnostic =>
                diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded);
            projected.Insert(0, new ExternalSourceConfigurationDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded,
                "Der letzte verifizierte Repository-Stand bleibt nur als Last-good-Nachweis verfügbar; ein aktueller Source-Snapshot wurde nicht erzeugt.",
                "warning",
                "$repository"));
        }

        return projected.ToImmutableArray();
    }

    internal static ExternalSourceRepositoryAcquisitionResult CreateRefreshFailure(
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        string? lastGoodRevision,
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified) =>
        ExternalSourceRepositoryAcquisitionResult.Failure(
            new ExternalSourceRepositoryAcquisitionFailureParameters(
                failureKind,
                diagnostics,
                ResolveFailureHealth(lastGoodRevision),
                NormalizeLastGoodRevision(lastGoodRevision),
                checkoutTrust));

    internal static ExternalSourceRepositoryAcquisitionResult FailureAfterCleanup(
        ExternalSourceCheckoutOwnership ownership,
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        string? lastGoodRevision = null) =>
        FailureAfterCleanup(new ExternalSourceRepositoryFailureAfterCleanupParameters(
            ownership,
            failureKind,
            diagnostics,
            lastGoodRevision,
            ExternalSourceCheckoutTrust.Unverified));

    internal static ExternalSourceRepositoryAcquisitionResult FailureAfterCleanup(
        ExternalSourceRepositoryFailureAfterCleanupParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Ownership);
        var resultDiagnostics = new List<ExternalSourceConfigurationDiagnostic>(parameters.Diagnostics);
        if (!parameters.Ownership.TryCleanup())
        {
            resultDiagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
                "Der eigene unvollständige Checkout konnte nicht vollständig bereinigt werden.",
                nameof(ExternalSourceRepositorySourcePolicy),
                "$repository"));
        }

        return CreateRefreshFailure(
            parameters.FailureKind,
            resultDiagnostics,
            parameters.LastGoodRevision,
            parameters.CheckoutTrust);
    }

    private static ExternalSourceRepositoryHealth ResolveFailureHealth(
        string? lastGoodRevision) =>
        ResolveDefaultHealth(false, NormalizeLastGoodRevision(lastGoodRevision));

    private static ExternalSourceRepositoryHealth ResolveDefaultHealth(
        bool isAvailable,
        string? normalizedLastGood) =>
        isAvailable
            ? ExternalSourceRepositoryHealth.Verified
            : normalizedLastGood is null
                ? ExternalSourceRepositoryHealth.Unavailable
                : ExternalSourceRepositoryHealth.Degraded;

    private static void ValidateHealthValue(
        ExternalSourceRepositoryHealth health,
        ExternalSourceRepositoryHealth? requestedHealth)
    {
        if (!Enum.IsDefined(health))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedHealth));
        }
    }

    private static void ValidateCheckoutTrust(
        bool isAvailable,
        ExternalSourceCheckoutTrust checkoutTrust)
    {
        if (!Enum.IsDefined(checkoutTrust)
            || isAvailable && checkoutTrust is not ExternalSourceCheckoutTrust.Clean
            || !isAvailable && checkoutTrust is ExternalSourceCheckoutTrust.Clean)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Der Checkout-Trustzustand stimmt nicht mit der Verfügbarkeit überein.",
                nameof(checkoutTrust));
        }
    }

    private static void ValidateAvailability(
        bool isAvailable,
        ExternalSourceRepositoryHealth health,
        ExternalSourceRepositoryHealth? requestedHealth)
    {
        if ((isAvailable && health is not ExternalSourceRepositoryHealth.Verified)
            || (!isAvailable && health is ExternalSourceRepositoryHealth.Verified))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Der Source-Healthzustand stimmt nicht mit der Verfügbarkeit überein.",
                nameof(requestedHealth));
        }
    }

    private static void ValidateLastGood(
        ExternalSourceRepositoryHealth health,
        string? normalizedLastGood,
        string? lastGoodRevision)
    {
        if (health is ExternalSourceRepositoryHealth.Degraded
            && normalizedLastGood is null)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein degraded Source-Ergebnis benötigt einen validierten Last-good-Commit.",
                nameof(lastGoodRevision));
        }

        if (health is not ExternalSourceRepositoryHealth.Degraded
            && normalizedLastGood is not null)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein Last-good-Commit darf nur degraded sichtbar sein.",
                nameof(lastGoodRevision));
        }
    }
}
