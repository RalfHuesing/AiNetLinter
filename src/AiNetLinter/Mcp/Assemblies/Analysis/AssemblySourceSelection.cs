#nullable enable

using System;
using System.Collections.Immutable;
using System.Threading;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;
using Microsoft.CodeAnalysis;

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
    CancellationToken CancellationToken);
