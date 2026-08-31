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
    internal const string WorkspaceDiagnosticFailureReason =
        "Die externe Solution hat beim Laden eine Workspace-Diagnose ausgelöst.";
    internal const string EmptySolutionFailureReason =
        "Die externe Solution enthält keine Projekte.";
    internal const string SolutionLoadFailureReason =
        "Die externe Solution konnte im restaurierten Checkout nicht geladen werden.";

    private readonly ExternalResourceRegistry? resources;
    private readonly IExternalSourceSnapshotResourceCoordinator? resourceCoordinator;

    internal ExternalSourceSnapshotMaterializer(
        ExternalResourceRegistry? resources = null,
        IExternalSourceSnapshotResourceCoordinator? resourceCoordinator = null)
    {
        this.resources = resources;
        this.resourceCoordinator = resourceCoordinator;
    }

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
                    cancellationToken)
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

    private async ValueTask<ExternalSourceSnapshot> MaterializeWithLeaseAsync(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceCheckoutMaterializationUse materializationUse,
        CancellationToken cancellationToken)
    {
        MSBuildWorkspace? workspace = null;
        ExternalResourceReservation? reservation = null;
        try
        {
            await VerifyCheckoutAsync(checkout, cancellationToken).ConfigureAwait(false);
            var resourceUsage = await ExternalSourceSnapshotResourceUsage.EstimateCheckoutAsync(
                    checkout.CheckoutPath,
                    cancellationToken)
                .ConfigureAwait(false);
            reservation = ReserveMaterializationBudget(mapping, checkout, resourceUsage);

            workspace = SourceFileCatalogLoader.CreateMSBuildWorkspace();
            var solution = await OpenSolutionAsync(workspace, checkout, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await VerifyCheckoutAsync(checkout, cancellationToken).ConfigureAwait(false);
            var snapshot = new ExternalSourceSnapshot(
                SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision),
                solution,
                workspace,
                new ExternalSourceSnapshotOwnership(
                    checkout,
                    materializationUse,
                    IsAttested: true,
                    ResourceUsage: resourceUsage,
                    ResourceReservation: reservation));
            reservation = null;
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
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                SolutionLoadFailureReason);
        }
        finally
        {
            reservation?.Dispose();
        }
    }

    private static async Task<Solution> OpenSolutionAsync(
        MSBuildWorkspace workspace,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken)
    {
        var workspaceFailed = 0;
        workspace.RegisterWorkspaceFailedHandler(_ => Interlocked.Exchange(ref workspaceFailed, 1));
        var solution = await workspace.OpenSolutionAsync(
                checkout.SolutionPath,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (Volatile.Read(ref workspaceFailed) != 0)
        {
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                WorkspaceDiagnosticFailureReason);
        }

        if (!solution.Projects.Any())
        {
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                EmptySolutionFailureReason);
        }

        return solution;
    }

    private ExternalResourceReservation? ReserveMaterializationBudget(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceSnapshotResourceUsage resourceUsage)
    {
        var request = new ExternalResourceRequest(
            SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision).StableValue,
            resourceUsage.DiskBytes,
            resourceUsage.MemoryBytes);
        if (resourceCoordinator is not null)
        {
            var succeeded = resourceCoordinator.TryReserveMaterialization(
                request,
                out var coordinatedReservation,
                out var coordinatedFailureReason);
            if (succeeded) return coordinatedReservation;
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                coordinatedFailureReason);
        }

        if (resources is null) return null;
        if (resources.TryReserve(request, out var reservation, out var failureReason)) return reservation;

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

internal static class ExternalSourceSnapshotMaterializationFailureReasons
{
    internal static bool IsSafe(string? reason) => reason is
        ExternalSourceSnapshotMaterializer.WorkspaceDiagnosticFailureReason
        or ExternalSourceSnapshotMaterializer.EmptySolutionFailureReason
        or ExternalSourceSnapshotMaterializer.SolutionLoadFailureReason;
}
