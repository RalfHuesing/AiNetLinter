#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal sealed class GiteaExternalSourceProvider : IExternalSourceProvider
{
    private readonly IExternalSourceRepositoryAcquirer acquirer;
    private readonly IExternalSourceSnapshotMaterializer materializer;

    internal GiteaExternalSourceProvider(
        IExternalSourceRepositoryAcquirer acquirer,
        IExternalSourceSnapshotMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(acquirer);
        ArgumentNullException.ThrowIfNull(materializer);
        this.acquirer = acquirer;
        this.materializer = materializer;
    }

    public async ValueTask<ExternalSourceProviderResult> ResolveAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        cancellationToken.ThrowIfCancellationRequested();

        ExternalSourceCheckoutHandle? checkout = null;
        ExternalSourceSnapshot? snapshot = null;
        try
        {
            var acquisition = await acquirer.AcquireAsync(
                mapping,
                cancellationToken).ConfigureAwait(false);

            if (!acquisition.IsAvailable)
            {
                return ExternalSourceProviderFailureProjection.FromUnavailableAcquisition(acquisition);
            }

            if (!TryGetCheckout(acquisition, out checkout))
            {
                return CreateMaterializationFailure(
                    snapshot: null,
                    checkout: checkout,
                    checkoutTrust: acquisition.CheckoutTrust is ExternalSourceCheckoutTrust.Dirty
                        ? ExternalSourceCheckoutTrust.Dirty
                        : ExternalSourceCheckoutTrust.Unverified);
            }

            // Der erfolgreich gelieferte Checkout-Handle wird vor jeder weiteren
            // Cancellation-Grenze an den lokalen Owner gebunden.
            cancellationToken.ThrowIfCancellationRequested();

            snapshot = await MaterializeVerifiedAsync(mapping, checkout!, cancellationToken)
                .ConfigureAwait(false);

            if (!IsValidSnapshot(snapshot, mapping, checkout!))
            {
                return CreateMaterializationFailure(
                    snapshot,
                    checkout);
            }

            return new ExternalSourceProviderResult(
                isAvailable: true,
                acquisition.Diagnostics,
                sourceSnapshot: snapshot);
        }
        catch (OperationCanceledException)
        {
            DisposeFailedResources(snapshot, checkout);
            throw;
        }
        catch (ExternalSourceSnapshotMaterializationException exception)
        {
            return CreateMaterializationFailure(
                snapshot,
                checkout,
                exception.CheckoutTrust,
                exception.FailureReason);
        }
        catch (Exception)
        {
            return CreateMaterializationFailure(snapshot, checkout);
        }
    }

    private async ValueTask<ExternalSourceSnapshot> MaterializeVerifiedAsync(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken)
    {
        ExternalSourceSnapshot? snapshot = null;
        try
        {
            snapshot = await materializer.MaterializeAsync(
                    mapping,
                    checkout,
                    cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var verification = await ExternalSourceCheckoutAttestation.VerifyCheckoutAsync(
                    checkout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!verification.IsVerified)
            {
                throw new ExternalSourceSnapshotMaterializationException(verification.Trust);
            }

            return snapshot;
        }
        catch
        {
            DisposeFailedResources(snapshot, null);
            throw;
        }
    }

    private static bool TryGetCheckout(
        ExternalSourceRepositoryAcquisitionResult acquisition,
        out ExternalSourceCheckoutHandle? checkout)
    {
        checkout = acquisition.Checkout;
        return checkout is not null
            && !checkout.IsDisposed
            && !string.IsNullOrWhiteSpace(acquisition.LoadedRevision)
            && string.Equals(
                acquisition.LoadedRevision,
                checkout.LoadedRevision,
                StringComparison.Ordinal);
    }

    private static bool IsValidSnapshot(
        ExternalSourceSnapshot? snapshot,
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout)
    {
        var expectedIdentity = SourceSnapshotIdentity.Create(
            mapping,
            checkout.LoadedRevision);
        return snapshot is not null
            && !snapshot.IsDisposed
            && snapshot.OwnsCheckout(checkout)
            && Equals(snapshot.Identity, expectedIdentity);
    }

    private static ExternalSourceProviderResult CreateMaterializationFailure(
        ExternalSourceSnapshot? snapshot,
        ExternalSourceCheckoutHandle? checkout = null,
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified,
        string? failureReason = null)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>
        {
            new(
                ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid,
                failureReason ?? string.Empty,
                "error",
                "$repository"),
        };
        DisposeFailedResources(snapshot, checkout);
        if (checkout?.CleanupState is ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed)
        {
            diagnostics.Add(new(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
                string.Empty,
                "error",
                "$repository"));
        }

        var projectedDiagnostics = ExternalSourceRepositoryFailurePolicy.ProjectTransportDiagnostics(
            diagnostics,
            isAvailable: false,
            ExternalSourceProviderFailureKind.InvalidResponse);
        if (ExternalSourceSnapshotMaterializationFailureReasons.IsSafe(failureReason))
        {
            projectedDiagnostics = projectedDiagnostics.Add(new(
                ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid,
                failureReason!,
                "error",
                "$repository"));
        }

        return new ExternalSourceProviderResult(
            isAvailable: false,
            projectedDiagnostics,
            state: ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.InvalidResponse,
                checkoutTrust: checkoutTrust));
    }

    private static void DisposeFailedResources(
        ExternalSourceSnapshot? snapshot,
        ExternalSourceCheckoutHandle? checkout)
    {
        try
        {
            snapshot?.Dispose();
        }
        catch (Exception ignored)
        {
            // Der Fallback darf weder Workspace- noch Checkout-Details exponieren.
            _ = ignored;
        }

        try
        {
            checkout?.Dispose();
        }
        catch (Exception ignored)
        {
            // ExternalSourceCheckoutHandle meldet Cleanup-Fehler über seinen Zustand.
            _ = ignored;
        }
    }
}
