#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;
using Microsoft.CodeAnalysis;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblySourceSelection
{
    private AssemblySourceSelection(
        SourceSnapshotLease sourceLease,
        ExternalSourceMatchResult matchResult,
        ExternalSourceRepositoryHealth providerHealth,
        ExternalSourceCheckoutTrust checkoutTrust,
        bool isAttested)
    {
        SourceLease = sourceLease;
        MatchResult = matchResult;
        ProviderHealth = providerHealth;
        CheckoutTrust = checkoutTrust;
        IsAttested = isAttested;
    }

    internal SourceSnapshotLease SourceLease { get; }

    internal ExternalSourceMatchResult MatchResult { get; }

    internal ExternalSourceRepositoryHealth ProviderHealth { get; }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }

    internal bool IsAttested { get; }

    internal AssemblySourceSelection? ForProject(
        SourceSnapshotLease projectLease,
        Project project)
    {
        ArgumentNullException.ThrowIfNull(projectLease);
        ArgumentNullException.ThrowIfNull(project);

        var assemblyName = project.AssemblyName ?? project.Name;
        var candidate = new ExternalSourceMatchCandidate(
            project.Id,
            project.Name,
            assemblyName,
            project.FilePath);
        var match = new ExternalSourceMatchResult
        {
            State = ExternalSourceMatchState.Matched,
            Confidence = ExternalSourceMatchConfidence.High,
            RequestedAssemblyAlias = assemblyName,
            SourceSnapshotIdentity = projectLease.Snapshot.Identity,
            MatchedCandidate = candidate,
            Candidates = ImmutableArray.Create(candidate),
            Evidence = MatchResult.Evidence.Add("project-reference-matched")
        };
        return Create(new AssemblySourceSelectionParameters(
            projectLease,
            match,
            ProviderHealth,
            CheckoutTrust,
            IsAttested));
    }

    internal static AssemblySourceSelection? Create(AssemblySourceSelectionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var sourceLease = parameters.SourceLease;
        var matchResult = parameters.MatchResult;
        ArgumentNullException.ThrowIfNull(sourceLease);
        ArgumentNullException.ThrowIfNull(matchResult);

        var sourceIdentity = sourceLease.Snapshot.Identity;
        if (matchResult.SourceSnapshotIdentity is null
            || !string.Equals(
                matchResult.SourceSnapshotIdentity.StableValue,
                sourceIdentity.StableValue,
                StringComparison.Ordinal)
            || sourceLease.IsDisposed
            || sourceLease.Snapshot.IsDisposed)
        {
            return null;
        }

        if (matchResult.State == ExternalSourceMatchState.Matched
            && matchResult.MatchedCandidate is null)
        {
            return null;
        }

        return new AssemblySourceSelection(
            sourceLease,
            matchResult,
            parameters.ProviderHealth,
            parameters.CheckoutTrust,
            parameters.IsAttested ?? sourceLease.Snapshot.IsAttested);
    }
}

internal sealed record AssemblySourceSelectionParameters(
    SourceSnapshotLease SourceLease,
    ExternalSourceMatchResult MatchResult,
    ExternalSourceRepositoryHealth ProviderHealth = ExternalSourceRepositoryHealth.Verified,
    ExternalSourceCheckoutTrust CheckoutTrust = ExternalSourceCheckoutTrust.Clean,
    bool? IsAttested = null);

internal sealed record AssemblyAnalysisContextRequest(
    string AssemblyPath,
    Solution? ConsumerSolution,
    string? ReceiverType,
    AssemblySourceSelection? SourceSelection,
    CancellationToken CancellationToken,
    string? FallbackReason = null,
    IReadOnlyList<ExternalSourceConfigurationDiagnostic>? SourceDiagnostics = null);

internal sealed class AssemblySourceProviderCreation
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken creationToken;
    private ExternalSourceProviderResult? completedResult;
    private int waiters;
    private int completed;
    private int snapshotAccepted;
    private int resultDisposed;
    private Task producerTask = Task.CompletedTask;

    internal AssemblySourceProviderCreation()
    {
        creationToken = cancellation.Token;
    }

    internal CancellationToken CreationToken => creationToken;

    internal Task ProducerTask => Volatile.Read(ref producerTask);

    internal void SetProducerTask(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Volatile.Write(ref producerTask, task);
    }

    internal TaskCompletionSource<ExternalSourceProviderResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void AddWaiter() => Interlocked.Increment(ref waiters);

    internal bool TrySetResult(ExternalSourceProviderResult result)
    {
        if (!Completion.TrySetResult(result)) return false;
        completedResult = result;
        return true;
    }

    internal void ReleaseWaiter(bool accepted)
    {
        if (accepted) Interlocked.Exchange(ref snapshotAccepted, 1);
        if (Interlocked.Decrement(ref waiters) == 0
            && Volatile.Read(ref completed) != 0
            && Volatile.Read(ref snapshotAccepted) == 0)
        {
            DisposeResultSnapshot();
        }
    }

    internal void Complete()
    {
        Interlocked.Exchange(ref completed, 1);
        if (Volatile.Read(ref waiters) == 0
            && Volatile.Read(ref snapshotAccepted) == 0)
        {
            DisposeResultSnapshot();
        }

        cancellation.Dispose();
    }

    internal void Cancel()
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Der Producer ist parallel fertig geworden; seine Completion ist bereits final.
        }

        Completion.TrySetCanceled(creationToken);
    }

    internal void DisposeRejectedResult(ExternalSourceProviderResult result)
    {
        ExternalSourceSnapshotDisposal.DisposeBestEffort(
            result.SourceSnapshot,
            "Provider-Creation nach Orchestrator-Dispose");
    }

    private void DisposeResultSnapshot()
    {
        if (Interlocked.Exchange(ref resultDisposed, 1) != 0) return;
        var result = Volatile.Read(ref completedResult);
        if (result is not null)
        {
            ExternalSourceSnapshotDisposal.DisposeBestEffort(
                result.SourceSnapshot,
                "Provider-Creation ohne Consumer-Lease");
        }
    }
}

internal static class ExternalSourceSnapshotDisposal
{
    internal static void DisposeBestEffort(ExternalSourceSnapshot? snapshot, string reason)
    {
        try
        {
            snapshot?.Dispose();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "External-Source-Snapshot konnte nicht vollständig freigegeben werden: Grund={Reason}", reason);
        }
    }
}

internal sealed class AssemblySourceProviderResultLease : IDisposable
{
    private readonly AssemblySourceProviderCreation creation;
    private int accepted;
    private int disposed;

    internal AssemblySourceProviderResultLease(
        AssemblySourceProviderCreation creation,
        ExternalSourceProviderResult result)
    {
        this.creation = creation;
        Result = result;
    }

    internal ExternalSourceProviderResult Result { get; }

    internal void AcceptSnapshot() => Interlocked.Exchange(ref accepted, 1);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            creation.ReleaseWaiter(Volatile.Read(ref accepted) != 0);
        }
    }
}
