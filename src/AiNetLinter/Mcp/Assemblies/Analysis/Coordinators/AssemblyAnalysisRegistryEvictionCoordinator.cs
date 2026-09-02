#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal sealed class AssemblyAnalysisRegistryEvictionCoordinator
{
    private readonly IAssemblyAnalysisEvictionResourceBudget resourceBudget;
    private readonly Func<Task<IReadOnlyList<AssemblyAnalysisEvictionCandidate>>> getCandidates;
    private readonly Func<AssemblyAnalysisEvictionCandidate, Task?> tryRetireCandidate;

    internal AssemblyAnalysisRegistryEvictionCoordinator(
        AssemblyAnalysisRegistryEvictionContext context)
    {
        resourceBudget = context.ResourceBudget;
        getCandidates = context.GetCandidates;
        tryRetireCandidate = context.TryRetireCandidate;
    }

    internal async Task<int> RunAsync(
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        var candidates = await getCandidates().ConfigureAwait(false);
        return await RunCoreAsync(candidates, forceCapacity, requiredPath, cancellationToken)
            .ConfigureAwait(false);
    }

    internal Task<int> RunTemporaryAsync(
        IReadOnlyList<AssemblyAnalysisEvictionCandidate> candidates,
        CancellationToken cancellationToken) =>
        RunCoreAsync(candidates, forceCapacity: true, requiredPath: null, cancellationToken);

    private async Task<int> RunCoreAsync(
        IReadOnlyList<AssemblyAnalysisEvictionCandidate> candidates,
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        var idleCandidates = FindIdleCandidates(candidates, forceCapacity);
        var retiredCount = await RetireIdleCandidatesAsync(
                idleCandidates,
                forceCapacity,
                requiredPath,
                cancellationToken)
            .ConfigureAwait(false);
        resourceBudget.EvictIdle();
        return retiredCount;
    }

    private List<AssemblyAnalysisEvictionCandidate> FindIdleCandidates(
        IReadOnlyList<AssemblyAnalysisEvictionCandidate> candidates,
        bool forceCapacity)
    {
        var now = resourceBudget.UtcNow;
        var idleCandidates = candidates
            .Where(candidate => forceCapacity
                ? candidate.IsIdleForCapacity()
                : candidate.IsIdle(now, resourceBudget.IdleTtl))
            .ToList();
        if (forceCapacity)
        {
            idleCandidates.Sort(static (left, right) =>
            {
                var comparison = left.LastUsedUtc.CompareTo(right.LastUsedUtc);
                return comparison != 0
                    ? comparison
                    : string.Compare(
                        left.CanonicalPath,
                        right.CanonicalPath,
                        StringComparison.OrdinalIgnoreCase);
            });
        }

        return idleCandidates;
    }

    private async Task<int> RetireIdleCandidatesAsync(
        IEnumerable<AssemblyAnalysisEvictionCandidate> candidates,
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        var retiredCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.BeforeRetirementAsync is not null)
            {
                await candidate.BeforeRetirementAsync().ConfigureAwait(false);
            }

            var retirement = tryRetireCandidate(candidate);
            if (retirement is null) continue;

            await retirement.ConfigureAwait(false);
            retiredCount++;
            if (forceCapacity
                && requiredPath is not null
                && resourceBudget.HasCapacity(requiredPath))
            {
                break;
            }
        }

        return retiredCount;
    }
}
