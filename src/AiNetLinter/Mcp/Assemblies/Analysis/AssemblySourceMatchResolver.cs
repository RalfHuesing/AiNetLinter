#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal enum ExternalSourceMatchState
{
    Matched,
    NoMatch,
    Ambiguous
}

internal enum ExternalSourceMatchConfidence
{
    None,
    High
}

internal sealed record ExternalSourceMatchCandidate(
    ProjectId ProjectId,
    string ProjectName,
    string AssemblyName,
    string? FilePath);

internal sealed record ExternalSourceMatchResult
{
    internal ExternalSourceMatchState State { get; init; }

    internal ExternalSourceMatchConfidence Confidence { get; init; }

    internal string RequestedAssemblyAlias { get; init; } = string.Empty;

    internal SourceSnapshotIdentity SourceSnapshotIdentity { get; init; } = null!;

    internal ExternalSourceMatchCandidate? MatchedCandidate { get; init; }

    internal ImmutableArray<ExternalSourceMatchCandidate> Candidates { get; init; }

    internal ImmutableArray<string> Evidence { get; init; }
}

internal static class AssemblySourceMatchResolver
{
    private const string SnapshotIdentityMatchedEvidence = "snapshot-identity-matched";
    private const string SnapshotIdentityMismatchedEvidence = "snapshot-identity-mismatched";
    private const string SnapshotUnavailableEvidence = "snapshot-unavailable";
    private const string MappingIdentityInvalidEvidence = "mapping-identity-invalid";
    private const string RequestedAliasInvalidEvidence = "requested-assembly-alias-invalid";
    private const string ExplicitAliasMatchedEvidence = "explicit-assembly-alias-matched";
    private const string ExplicitAliasNotConfiguredEvidence = "explicit-assembly-alias-not-configured";
    private const string ProjectAssemblyNameMatchedEvidence = "project-assembly-name-matched";
    private const string ProjectAssemblyNameNotMatchedEvidence = "project-assembly-name-not-matched";
    private const string UniqueProjectMatchedEvidence = "unique-project-matched";
    private const string DuplicateProjectAssemblyNameEvidence = "duplicate-project-assembly-name";

    internal static ExternalSourceMatchResult Resolve(
        SourceSnapshotLease sourceLease,
        ExternalSourceMapping mapping,
        string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(sourceLease);
        ArgumentNullException.ThrowIfNull(mapping);

        var snapshot = sourceLease.Snapshot;
        var snapshotIdentity = snapshot.Identity;
        var requestedAlias = NormalizeAssemblyAlias(assemblyName) ?? string.Empty;

        if (snapshot.IsDisposed)
        {
            return NoMatch(
                requestedAlias,
                snapshotIdentity,
                [SnapshotUnavailableEvidence]);
        }

        if (!TryMatchesMappingIdentity(mapping, snapshotIdentity, out var mappingIdentityInvalid))
        {
            return NoMatch(
                requestedAlias,
                snapshotIdentity,
                [mappingIdentityInvalid
                    ? MappingIdentityInvalidEvidence
                    : SnapshotIdentityMismatchedEvidence]);
        }

        var evidence = new List<string> { SnapshotIdentityMatchedEvidence };
        if (requestedAlias.Length == 0)
        {
            evidence.Add(RequestedAliasInvalidEvidence);
            return NoMatch(requestedAlias, snapshotIdentity, evidence);
        }

        if (!mapping.Assemblies.Any(alias =>
                string.Equals(NormalizeAssemblyAlias(alias), requestedAlias, StringComparison.OrdinalIgnoreCase)))
        {
            evidence.Add(ExplicitAliasNotConfiguredEvidence);
            return NoMatch(requestedAlias, snapshotIdentity, evidence);
        }

        return ResolveProjects(snapshot.Solution.Projects, requestedAlias, snapshotIdentity);
    }

    private static ExternalSourceMatchResult ResolveProjects(
        IEnumerable<Project> projects,
        string requestedAlias,
        SourceSnapshotIdentity snapshotIdentity)
    {
        var evidence = new List<string>
        {
            SnapshotIdentityMatchedEvidence,
            ExplicitAliasMatchedEvidence
        };
        var candidates = projects
            .Select(project => CreateCandidate(project, requestedAlias))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ProjectName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.AssemblyName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.ProjectId.ToString(), StringComparer.Ordinal)
            .ToImmutableArray();

        if (candidates.IsEmpty)
        {
            evidence.Add(ProjectAssemblyNameNotMatchedEvidence);
            return NoMatch(requestedAlias, snapshotIdentity, evidence);
        }

        evidence.Add(ProjectAssemblyNameMatchedEvidence);
        if (candidates.Length > 1)
        {
            evidence.Add(DuplicateProjectAssemblyNameEvidence);
            return new ExternalSourceMatchResult
            {
                State = ExternalSourceMatchState.Ambiguous,
                Confidence = ExternalSourceMatchConfidence.None,
                RequestedAssemblyAlias = requestedAlias,
                SourceSnapshotIdentity = snapshotIdentity,
                MatchedCandidate = null,
                Candidates = candidates,
                Evidence = evidence.ToImmutableArray()
            };
        }

        evidence.Add(UniqueProjectMatchedEvidence);
        return new ExternalSourceMatchResult
        {
            State = ExternalSourceMatchState.Matched,
            Confidence = ExternalSourceMatchConfidence.High,
            RequestedAssemblyAlias = requestedAlias,
            SourceSnapshotIdentity = snapshotIdentity,
            MatchedCandidate = candidates[0],
            Candidates = candidates,
            Evidence = evidence.ToImmutableArray()
        };
    }

    private static ExternalSourceMatchCandidate? CreateCandidate(
        Project project,
        string requestedAlias)
    {
        var normalizedAssemblyName = NormalizeAssemblyAlias(project.AssemblyName);
        return normalizedAssemblyName is not null
            && string.Equals(normalizedAssemblyName, requestedAlias, StringComparison.OrdinalIgnoreCase)
            ? new ExternalSourceMatchCandidate(
                project.Id,
                project.Name,
                project.AssemblyName!,
                project.FilePath)
            : null;
    }

    private static bool TryMatchesMappingIdentity(
        ExternalSourceMapping mapping,
        SourceSnapshotIdentity snapshotIdentity,
        out bool mappingIdentityInvalid)
    {
        try
        {
            var expectedIdentity = SourceSnapshotIdentity.Create(mapping, snapshotIdentity.LoadedRevision);
            mappingIdentityInvalid = false;
            return string.Equals(
                       expectedIdentity.RepositoryUrl,
                       snapshotIdentity.RepositoryUrl,
                       StringComparison.Ordinal)
                   && string.Equals(
                       expectedIdentity.SolutionPath,
                       snapshotIdentity.SolutionPath,
                       StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            mappingIdentityInvalid = true;
            return false;
        }
    }

    private static ExternalSourceMatchResult NoMatch(
        string requestedAlias,
        SourceSnapshotIdentity snapshotIdentity,
        IEnumerable<string> evidence) =>
        new ExternalSourceMatchResult
        {
            State = ExternalSourceMatchState.NoMatch,
            Confidence = ExternalSourceMatchConfidence.None,
            RequestedAssemblyAlias = requestedAlias,
            SourceSnapshotIdentity = snapshotIdentity,
            MatchedCandidate = null,
            Candidates = ImmutableArray<ExternalSourceMatchCandidate>.Empty,
            Evidence = evidence.ToImmutableArray()
        };

    private static string? NormalizeAssemblyAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4].Trim();
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
