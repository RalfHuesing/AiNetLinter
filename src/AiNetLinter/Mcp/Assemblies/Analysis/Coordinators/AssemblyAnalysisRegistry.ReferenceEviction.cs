#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal sealed class AssemblyAnalysisRegistryEvictionCandidates
{
    private readonly Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCreation>>> getCreations;
    private readonly Func<IAssemblyAnalysisEvictionEntry, Task>? beforeRetirementAsync;
    private readonly Func<IAssemblyAnalysisEvictionEntry, bool> isTemporaryReferenceEvictionRequested;
    private readonly Action<IAssemblyAnalysisEvictionEntry> clearTemporaryReferenceEvictionRequest;

    internal AssemblyAnalysisRegistryEvictionCandidates(
        Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCreation>>> getCreations,
        Func<IAssemblyAnalysisEvictionEntry, Task>? beforeRetirementAsync,
        Func<IAssemblyAnalysisEvictionEntry, bool> isTemporaryReferenceEvictionRequested,
        Action<IAssemblyAnalysisEvictionEntry> clearTemporaryReferenceEvictionRequest)
    {
        this.getCreations = getCreations;
        this.beforeRetirementAsync = beforeRetirementAsync;
        this.isTemporaryReferenceEvictionRequested = isTemporaryReferenceEvictionRequested;
        this.clearTemporaryReferenceEvictionRequest = clearTemporaryReferenceEvictionRequest;
    }

    internal Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>> GetCompletedEvictionCandidates() =>
        GetCompletedEvictionCandidates(temporaryOnly: false);

    internal Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>> GetCompletedTemporaryEvictionCandidates() =>
        GetCompletedEvictionCandidates(temporaryOnly: true);

    private async Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>> GetCompletedEvictionCandidates(
        bool temporaryOnly)
    {
        var creations = await getCreations().ConfigureAwait(false);
        return creations
            .Where(creation => !temporaryOnly || isTemporaryReferenceEvictionRequested(creation.Entry))
            .Select(CreateCandidate)
            .ToList();
    }

    private AssemblyAnalysisEvictionCandidate CreateCandidate(AssemblyAnalysisEvictionCreation creation)
    {
        var entry = creation.Entry;
        return new(
            creation.Key,
            entry.CanonicalPath,
            entry.LastUsedUtc,
            entry.IsIdleForCapacity,
            entry.IsIdle,
            creation.TryRetire,
            beforeRetirementAsync is null ? null : () => beforeRetirementAsync(entry),
            () => clearTemporaryReferenceEvictionRequest(entry));
    }

    internal Task? TryRetireCandidate(AssemblyAnalysisEvictionCandidate candidate)
    {
        var retirement = candidate.TryRetire();
        if (retirement is not null) candidate.OnRetired?.Invoke();
        return retirement;
    }
}

internal sealed class AssemblyAnalysisRegistryReferenceEviction
{
    private readonly AssemblyAnalysisRegistryEvictionCoordinator evictionCoordinator;
    private readonly Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>>> getCandidates;
    private readonly object requestGate = new();
    private readonly HashSet<IAssemblyAnalysisEvictionEntry> requests = [];

    internal AssemblyAnalysisRegistryReferenceEviction(
        AssemblyAnalysisRegistryEvictionCoordinator evictionCoordinator,
        Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>>> getCandidates)
    {
        this.evictionCoordinator = evictionCoordinator;
        this.getCandidates = getCandidates;
    }

    internal void Request(IAssemblyAnalysisEvictionEntry entry)
    {
        lock (requestGate)
        {
            if (!entry.IsRetiring) requests.Add(entry);
        }
    }

    internal int RequestCount
    {
        get
        {
            lock (requestGate) return requests.Count;
        }
    }

    internal bool IsRequested(IAssemblyAnalysisEvictionEntry entry)
    {
        lock (requestGate) return requests.Contains(entry);
    }

    internal void ClearRequest(IAssemblyAnalysisEvictionEntry entry)
    {
        lock (requestGate) requests.Remove(entry);
    }

    internal async Task<int> EvictAsync(CancellationToken cancellationToken)
    {
        var candidates = await getCandidates().ConfigureAwait(false);
        return await evictionCoordinator
            .RunTemporaryAsync(candidates, cancellationToken)
            .ConfigureAwait(false);
    }
}
