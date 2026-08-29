#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceSnapshotMaterializer : IExternalSourceSnapshotMaterializer
{
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

        MSBuildWorkspace? workspace = null;
        try
        {
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

            var snapshot = new ExternalSourceSnapshot(
                SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision),
                solution,
                workspace,
                checkout);
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
    internal ExternalSourceSnapshotMaterializationException()
        : base("Die externe Source-Solution konnte nicht vollständig materialisiert werden.")
    {
    }
}
