#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal enum ExternalSourceCheckoutCleanupState
{
    NotAttempted,
    Succeeded,
    RepositoryCleanupFailed,
    Quarantined,
}

internal sealed record ExternalSourceCheckoutQuarantine(
    string CheckoutPath,
    string Owner,
    string Reason,
    DateTime CreatedUtc,
    DateTime ExpiresUtc,
    string MetadataPath);

internal sealed class ExternalSourceCheckoutOwnership
{
    internal const string OwnershipMarkerFileName = ".ainetlinter-owner";

    internal ExternalSourceCheckoutOwnership(
        string stagingRoot,
        string checkoutPath,
        string ownershipToken)
    {
        StagingRoot = stagingRoot;
        CheckoutPath = checkoutPath;
        OwnershipToken = ownershipToken;
    }

    internal string StagingRoot { get; }

    internal string CheckoutPath { get; }

    internal string OwnershipToken { get; }

    internal string OwnershipMarkerPath =>
        Path.Combine(CheckoutPath, OwnershipMarkerFileName);

    internal bool HasValidToken()
    {
        try
        {
            return File.Exists(OwnershipMarkerPath)
                && string.Equals(
                    File.ReadAllText(OwnershipMarkerPath),
                    OwnershipToken,
                    StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    internal bool TryCleanup() =>
        ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout(this);

    internal bool TryCleanupOrQuarantine(
        string reason,
        out ExternalSourceCheckoutQuarantine? quarantine)
    {
        if (TryCleanup())
        {
            quarantine = null;
            return true;
        }

        quarantine = TryQuarantine(reason, TimeSpan.FromHours(24));
        return false;
    }

    internal ExternalSourceCheckoutQuarantine? TryQuarantine(
        string reason,
        TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(reason) || ttl <= TimeSpan.Zero) return null;
        try
        {
            var quarantineRoot = Path.Combine(StagingRoot, ".quarantine");
            Directory.CreateDirectory(quarantineRoot);
            var createdUtc = DateTime.UtcNow;
            var metadataPath = Path.Combine(
                quarantineRoot,
                $"checkout-{Guid.NewGuid():N}.json");
            var quarantine = new ExternalSourceCheckoutQuarantine(
                CheckoutPath,
                "ainetlinter-source-acquirer",
                reason,
                createdUtc,
                createdUtc.Add(ttl),
                metadataPath);
            File.WriteAllText(
                metadataPath,
                JsonSerializer.Serialize(quarantine));
            return quarantine;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception)
            || exception is JsonException)
        {
            return null;
        }
    }
}

internal sealed class ExternalSourceCheckoutHandle : IExternalSourceCheckoutOwner
{
    private readonly ExternalSourceCheckoutOwnership ownership;
    private readonly object lifecycleGate = new();
    private ExternalSourceCheckoutMaterializationLease? materializationLease;
    private int activeMaterializationUses;
    private int disposed;
    private int cleanupState;
    private ExternalSourceCheckoutQuarantine? quarantine;

    internal ExternalSourceCheckoutHandle(
        ExternalSourceCheckoutOwnership ownership,
        string solutionPath,
        string loadedRevision,
        ExternalSourceCheckoutAttestation? checkoutAttestation = null)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        this.ownership = ownership;
        SolutionPath = solutionPath;
        LoadedRevision = loadedRevision;
        if (checkoutAttestation is not null
            && !string.Equals(
                checkoutAttestation.ExpectedRevision,
                loadedRevision,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Die Checkout-Attestation gehört nicht zur geladenen Revision.",
                nameof(checkoutAttestation));
        }

        CheckoutAttestation = checkoutAttestation;
    }

    internal string CheckoutPath => ownership.CheckoutPath;

    internal ExternalSourceCheckoutOwnership Ownership => ownership;

    internal string SolutionPath { get; }

    internal string LoadedRevision { get; }

    internal ExternalSourceCheckoutAttestation? CheckoutAttestation { get; }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal ExternalSourceCheckoutCleanupState CleanupState =>
        (ExternalSourceCheckoutCleanupState)Volatile.Read(ref cleanupState);

    internal ExternalSourceCheckoutQuarantine? Quarantine => Volatile.Read(ref quarantine);

    internal ExternalSourceCheckoutMaterializationUse? TryAcquireMaterializationUse(
        CancellationToken cancellationToken)
    {
        lock (lifecycleGate)
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return null;
            }

            if (materializationLease is null
                && !ExternalSourceCheckoutMaterializationLease.TryAcquire(
                    ownership,
                    cancellationToken,
                    out materializationLease))
            {
                return null;
            }

            activeMaterializationUses++;
            return new ExternalSourceCheckoutMaterializationUse(ReleaseMaterializationUse);
        }
    }

    public void Dispose()
    {
        ExternalSourceCheckoutMaterializationLease? leaseToRelease = null;
        lock (lifecycleGate)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            if (activeMaterializationUses == 0)
            {
                leaseToRelease = materializationLease;
                materializationLease = null;
            }
        }

        leaseToRelease?.Dispose();
        var cleanupSucceeded = ownership.TryCleanup();
        if (!cleanupSucceeded)
        {
            Volatile.Write(ref quarantine, ownership.TryQuarantine(
                "Checkout konnte nach Freigabe nicht sicher bereinigt werden.",
                TimeSpan.FromHours(24)));
        }
        Volatile.Write(
            ref cleanupState,
            (int)(cleanupSucceeded
                ? ExternalSourceCheckoutCleanupState.Succeeded
                : Quarantine is not null
                    ? ExternalSourceCheckoutCleanupState.Quarantined
                    : ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed));
    }

    private void ReleaseMaterializationUse()
    {
        ExternalSourceCheckoutMaterializationLease? leaseToRelease = null;
        lock (lifecycleGate)
        {
            if (activeMaterializationUses == 0)
            {
                return;
            }

            activeMaterializationUses--;
            if (activeMaterializationUses == 0 && Volatile.Read(ref disposed) != 0)
            {
                leaseToRelease = materializationLease;
                materializationLease = null;
            }
        }

        if (leaseToRelease is not null)
        {
            leaseToRelease.Dispose();
            var cleanupSucceeded = ownership.TryCleanup();
            if (!cleanupSucceeded)
            {
                Volatile.Write(ref quarantine, ownership.TryQuarantine(
                    "Checkout konnte nach letzter Materialisierungsnutzung nicht sicher bereinigt werden.",
                    TimeSpan.FromHours(24)));
            }
            Volatile.Write(
                ref cleanupState,
                (int)(cleanupSucceeded
                    ? ExternalSourceCheckoutCleanupState.Succeeded
                    : Quarantine is not null
                        ? ExternalSourceCheckoutCleanupState.Quarantined
                        : ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed));
        }
    }
}

internal sealed record ExternalSourceRepositoryAcquisitionFailureParameters(
    ExternalSourceProviderFailureKind FailureKind,
    IEnumerable<ExternalSourceConfigurationDiagnostic> Diagnostics,
    ExternalSourceRepositoryHealth Health,
    string? LastGoodRevision,
    ExternalSourceCheckoutTrust CheckoutTrust);

internal sealed record ExternalSourceRepositoryTransportExecutionParameters(
    ExternalSourceMapping Mapping,
    string CheckoutPath,
    CancellationToken CancellationToken,
    Func<ExternalSourceMapping, string, CancellationToken, ValueTask<ExternalSourceRepositoryTransportResult>> Execute,
    string FailureMessage);

internal static class ExternalSourceRepositoryTransportExecution
{
    internal static async Task<ExternalSourceRepositoryTransportResult> ExecuteAsync(
        ExternalSourceRepositoryTransportExecutionParameters parameters)
    {
        try
        {
            return await parameters.Execute(
                parameters.Mapping,
                parameters.CheckoutPath,
                parameters.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceRepositoryFailurePolicy.GetTransportDiagnosticCode(exception),
                    parameters.FailureMessage,
                    nameof(ExternalSourceRepositoryTransportExecution),
                    "$repository")],
                state: ExternalSourceRepositoryResultState.Create(
                    ExternalSourceRepositoryFailurePolicy.ClassifyTransportException(exception)));
        }
    }
}

internal sealed record ExternalSourceRepositoryAcquisitionResult
{
    private ExternalSourceRepositoryAcquisitionResult(
        bool isAvailable,
        ExternalSourceCheckoutHandle? checkout,
        string? loadedRevision,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceRepositoryResultState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IsAvailable = isAvailable;
        Checkout = checkout;
        LoadedRevision = loadedRevision;
        FailureKind = state.FailureKind;
        var validatedState = ExternalSourceRepositorySourcePolicy.ValidateResultState(isAvailable, state);
        Health = validatedState.Health;
        LastGoodRevision = validatedState.LastGoodRevision;
        CheckoutTrust = validatedState.CheckoutTrust;
        Diagnostics = isAvailable
            ? diagnostics.ToImmutableArray()
            : ExternalSourceRepositorySourcePolicy.ProjectFailureDiagnostics(
                diagnostics,
                Health,
                FailureKind);
    }

    internal bool IsAvailable { get; }

    internal ExternalSourceCheckoutHandle? Checkout { get; }

    internal string? LoadedRevision { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }

    internal ExternalSourceRepositoryHealth Health { get; }

    internal string? LastGoodRevision { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal static ExternalSourceRepositoryAcquisitionResult Success(
        ExternalSourceCheckoutHandle checkout,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        if (checkout.CheckoutAttestation is null)
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verifiziertes Akquisitionsergebnis benötigt eine Checkout-Attestation.",
                nameof(checkout));
        }

        if (!ExternalSourceRepositoryCacheKey.IsSafeRevision(checkout.LoadedRevision))
        {
            throw new ArgumentException(
                // ainetlinter-disable MagicValues — interner Vertragsfehler, keine lokalisierbare Nutzermeldung.
                "Ein verifiziertes Akquisitionsergebnis benötigt eine sichere Revision.",
                nameof(checkout));
        }

        return new(
            true,
            checkout,
            checkout.LoadedRevision,
            diagnostics,
            ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.None,
                ExternalSourceRepositoryHealth.Verified,
                checkoutTrust: ExternalSourceCheckoutTrust.Clean));
    }

    internal static ExternalSourceRepositoryAcquisitionResult Failure(
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
        ExternalSourceRepositoryHealth health = ExternalSourceRepositoryHealth.Unavailable,
        string? lastGoodRevision = null) =>
        Failure(new ExternalSourceRepositoryAcquisitionFailureParameters(
            failureKind,
            diagnostics,
            health,
            lastGoodRevision,
            ExternalSourceCheckoutTrust.Unverified));

    internal static ExternalSourceRepositoryAcquisitionResult Failure(
        ExternalSourceRepositoryAcquisitionFailureParameters parameters)
        => new(
        false,
        null,
        null,
        parameters.Diagnostics,
        ExternalSourceRepositoryResultState.Create(
            parameters.FailureKind is ExternalSourceProviderFailureKind.None
                ? ExternalSourceProviderFailureKind.InvalidResponse
                : parameters.FailureKind,
            parameters.Health,
            parameters.LastGoodRevision,
            parameters.CheckoutTrust));
}
