#nullable enable

using System;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed record AssemblySourceSelection
{
    private AssemblySourceSelection(
        SourceSnapshotLease sourceLease,
        ExternalSourceMatchResult matchResult)
    {
        SourceLease = sourceLease;
        MatchResult = matchResult;
    }

    internal SourceSnapshotLease SourceLease { get; }

    internal ExternalSourceMatchResult MatchResult { get; }

    internal static AssemblySourceSelection? Create(
        SourceSnapshotLease sourceLease,
        ExternalSourceMatchResult matchResult)
    {
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

        return new AssemblySourceSelection(sourceLease, matchResult);
    }
}

internal sealed record AssemblyAnalysisContextRequest(
    string AssemblyPath,
    Solution? ConsumerSolution,
    string? ReceiverType,
    AssemblySourceSelection? SourceSelection,
    CancellationToken CancellationToken);
