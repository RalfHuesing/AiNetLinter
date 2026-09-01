#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    internal const string NoCSharpDocumentsFailureReason =
        "Die externe Solution enthält keine nutzbaren C#-Dokumente.";
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
            var opened = await OpenSolutionAsync(workspace, checkout, cancellationToken).ConfigureAwait(false);
            var solution = opened.Solution;
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
                    ResourceReservation: reservation),
                opened.Diagnostics);
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

    private static async Task<OpenedSourceSolution> OpenSolutionAsync(
        MSBuildWorkspace workspace,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            var message = string.Join(' ', (args.Diagnostic.Message ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            lock (diagnostics)
            {
                if (diagnostics.Count < 20)
                {
                    diagnostics.Add(new(
                        ExternalSourceConfigurationDiagnosticCodes.WorkspaceDiagnostic,
                        TruncateDiagnostic(message),
                        "warning",
                        "$workspace"));
                }
            }
        });
        var solution = await workspace.OpenSolutionAsync(
                checkout.SolutionPath,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!solution.Projects.Any())
        {
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                EmptySolutionFailureReason,
                diagnostics);
        }

        if (!solution.Projects.Any(project =>
                string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal)
                && project.Documents.Any()))
        {
            diagnostics.Add(new(
                ExternalSourceConfigurationDiagnosticCodes.NoCSharpDocuments,
                NoCSharpDocumentsFailureReason,
                "error",
                "$solution"));
            throw new ExternalSourceSnapshotMaterializationException(
                checkoutTrust: ExternalSourceCheckoutTrust.Clean,
                NoCSharpDocumentsFailureReason,
                diagnostics);
        }

        return new(solution, diagnostics.ToImmutableArray());
    }

    private static string TruncateDiagnostic(string message)
    {
        if (message.Length <= 256) return message;
        return message[..255] + "…";
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
        string? failureReason = null,
        IEnumerable<ExternalSourceConfigurationDiagnostic>? diagnostics = null)
        : base("Die externe Source-Solution konnte nicht vollständig materialisiert werden.")
    {
        CheckoutTrust = ExternalSourceRepositorySourcePolicy.NormalizeFailureTrust(checkoutTrust);
        FailureReason = failureReason;
        Diagnostics = (diagnostics ?? Array.Empty<ExternalSourceConfigurationDiagnostic>())
            .Distinct()
            .Take(20)
            .ToImmutableArray();
    }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }

    internal string? FailureReason { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }
}

internal static class ExternalSourceSnapshotMaterializationFailureReasons
{
    internal static bool IsSafe(string? reason) => reason is
        ExternalSourceSnapshotMaterializer.WorkspaceDiagnosticFailureReason
        or ExternalSourceSnapshotMaterializer.EmptySolutionFailureReason
        or ExternalSourceSnapshotMaterializer.NoCSharpDocumentsFailureReason
        or ExternalSourceSnapshotMaterializer.SolutionLoadFailureReason;
}

internal sealed record OpenedSourceSolution(
    Solution Solution,
    ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics);
