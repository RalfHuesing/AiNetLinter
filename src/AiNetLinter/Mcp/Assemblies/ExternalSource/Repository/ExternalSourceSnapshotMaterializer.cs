#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed class ExternalSourceSnapshotMaterializer : IExternalSourceSnapshotMaterializer
{
    private readonly ExternalResourceRegistry? resources;

    internal ExternalSourceSnapshotMaterializer(ExternalResourceRegistry? resources = null) =>
        this.resources = resources;

    public async ValueTask<ExternalSourceSnapshot> MaterializeAsync(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(checkout);
        cancellationToken.ThrowIfCancellationRequested();
        if (checkout.IsDisposed)
        {
            throw new ExternalSourceSnapshotMaterializationException();
        }

        var materializationUse = checkout.TryAcquireMaterializationUse(cancellationToken);
        if (materializationUse is null)
        {
            throw new ExternalSourceSnapshotMaterializationException();
        }

        ExternalSourceSnapshot? snapshot = null;
        try
        {
            snapshot = await MaterializeWithLeaseAsync(
                    mapping,
                    checkout,
                    materializationUse,
                    cancellationToken,
                    resources)
                .ConfigureAwait(false);
            return snapshot;
        }
        finally
        {
            if (snapshot is null)
            {
                materializationUse.Dispose();
            }
        }
    }

    private static async ValueTask<ExternalSourceSnapshot> MaterializeWithLeaseAsync(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceCheckoutMaterializationUse materializationUse,
        CancellationToken cancellationToken,
        ExternalResourceRegistry? resources)
    {
        MSBuildWorkspace? workspace = null;
        ExternalResourceReservation? reservation = null;
        try
        {
            await VerifyCheckoutAsync(checkout, cancellationToken).ConfigureAwait(false);
            var resourceUsage = ExternalSourceSnapshotResourceUsage.EstimateCheckout(checkout.CheckoutPath);
            reservation = ReserveMaterializationBudget(resources, mapping, checkout, resourceUsage);

            workspace = SourceFileCatalogLoader.CreateMSBuildWorkspace();
            var workspaceFailed = 0;
            workspace.RegisterWorkspaceFailedHandler(_ => Interlocked.Exchange(ref workspaceFailed, 1));

            var solution = await workspace.OpenSolutionAsync(
                checkout.SolutionPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref workspaceFailed) != 0 || !solution.Projects.Any())
            {
                throw new ExternalSourceSnapshotMaterializationException();
            }

            await VerifyCheckoutAsync(checkout, cancellationToken).ConfigureAwait(false);
            var snapshot = new ExternalSourceSnapshot(
                SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision),
                solution,
                workspace,
                new ExternalSourceSnapshotOwnership(
                    checkout,
                    materializationUse,
                    IsAttested: true,
                    ResourceUsage: resourceUsage));
            workspace = null;
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            DisposeWorkspace(workspace);
            throw;
        }
        catch (ExternalSourceSnapshotMaterializationException)
        {
            DisposeWorkspace(workspace);
            throw;
        }
        catch (Exception)
        {
            DisposeWorkspace(workspace);
            throw new ExternalSourceSnapshotMaterializationException();
        }
        finally
        {
            reservation?.Dispose();
        }
    }

    private static ExternalResourceReservation? ReserveMaterializationBudget(
        ExternalResourceRegistry? resources,
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceSnapshotResourceUsage resourceUsage)
    {
        if (resources is null) return null;
        var request = new ExternalResourceRequest(
            SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision).StableValue,
            resourceUsage.DiskBytes,
            resourceUsage.MemoryBytes);
        if (resources.TryReserve(request, out var reservation, out var failureReason))
        {
            return reservation;
        }

        throw new ExternalSourceSnapshotMaterializationException(
            checkoutTrust: ExternalSourceCheckoutTrust.Clean,
            failureReason);
    }

    private static async ValueTask VerifyCheckoutAsync(
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken)
    {
        var verification = await ExternalSourceCheckoutAttestation.VerifyCheckoutAsync(
                checkout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!verification.IsVerified)
        {
            throw new ExternalSourceSnapshotMaterializationException(verification.Trust);
        }
    }

    private static void DisposeWorkspace(MSBuildWorkspace? workspace)
    {
        try
        {
            workspace?.Dispose();
        }
        catch (Exception ignored)
        {
            // Der Provider übernimmt den Checkout auch dann, wenn der Workspace-Cleanup fehlschlägt.
            _ = ignored;
        }
    }
}

internal sealed class ExternalSourceSnapshotMaterializationException : Exception
{
    internal ExternalSourceSnapshotMaterializationException(
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified,
        string? failureReason = null)
        : base("Die externe Source-Solution konnte nicht vollständig materialisiert werden.")
    {
        CheckoutTrust = ExternalSourceRepositorySourcePolicy.NormalizeFailureTrust(checkoutTrust);
        FailureReason = failureReason;
    }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }

    internal string? FailureReason { get; }
}
