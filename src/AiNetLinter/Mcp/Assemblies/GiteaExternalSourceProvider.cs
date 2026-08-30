#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

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
            cancellationToken.ThrowIfCancellationRequested();

            if (!acquisition.IsAvailable)
            {
                return ExternalSourceProviderFailureProjection.FromUnavailableAcquisition(acquisition);
            }

            checkout = acquisition.Checkout;
            if (checkout is null
                || checkout.IsDisposed
                || string.IsNullOrWhiteSpace(acquisition.LoadedRevision)
                || !string.Equals(
                    acquisition.LoadedRevision,
                    checkout.LoadedRevision,
                    StringComparison.Ordinal))
            {
                return CreateMaterializationFailure(snapshot: null, checkout: checkout);
            }

            snapshot = await materializer.MaterializeAsync(
                mapping,
                checkout,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var expectedIdentity = SourceSnapshotIdentity.Create(
                mapping,
                checkout.LoadedRevision);
            if (snapshot is null
                || snapshot.IsDisposed
                || !snapshot.OwnsCheckout(checkout)
                || !Equals(snapshot.Identity, expectedIdentity))
            {
                return CreateMaterializationFailure(snapshot, checkout);
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
        catch (Exception)
        {
            return CreateMaterializationFailure(snapshot, checkout);
        }
    }

    private static ExternalSourceProviderResult CreateMaterializationFailure(
        ExternalSourceSnapshot? snapshot,
        ExternalSourceCheckoutHandle? checkout = null)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>
        {
            new(
                ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid,
                string.Empty,
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

        return new ExternalSourceProviderResult(
            isAvailable: false,
            ExternalSourceRepositoryFailurePolicy.ProjectTransportDiagnostics(
                diagnostics,
                isAvailable: false,
                ExternalSourceProviderFailureKind.InvalidResponse),
            state: ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.InvalidResponse));
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
