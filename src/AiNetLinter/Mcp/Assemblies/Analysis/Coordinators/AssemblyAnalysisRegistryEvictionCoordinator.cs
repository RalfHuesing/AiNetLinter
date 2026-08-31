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
    private readonly Lock gate;
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries;
    private readonly List<Task> retiredEntries;
    private readonly Func<bool> isDisposed;
    private readonly IAssemblyAnalysisEvictionResourceBudget resourceBudget;
    private readonly Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync;
    private readonly Func<AssemblyAnalysisRegistryEntryCreation, Task> retireEntryAsync;

    internal AssemblyAnalysisRegistryEvictionCoordinator(
        AssemblyAnalysisRegistryEvictionContext context)
    {
        gate = context.Gate;
        entries = context.Entries;
        retiredEntries = context.RetiredEntries;
        isDisposed = context.IsDisposed;
        resourceBudget = context.ResourceBudget;
        beforeRetirementAsync = context.BeforeRetirementAsync;
        retireEntryAsync = context.RetireEntryAsync;
    }

    internal Task<int> RunAsync(
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken) =>
        RunCoreAsync(forceCapacity, requiredPath, cancellationToken);

    private async Task<int> RunCoreAsync(
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        var idleCandidates = await FindIdleCandidatesAsync(forceCapacity).ConfigureAwait(false);
        var retiredCount = await RetireIdleCandidatesAsync(
                idleCandidates,
                forceCapacity,
                requiredPath,
                cancellationToken)
            .ConfigureAwait(false);
        resourceBudget.EvictIdle();
        return retiredCount;
    }

    private async Task<List<(AssemblyAnalysisRegistryEntryCreation Creation, AssemblyAnalysisEntry Entry)>>
        FindIdleCandidatesAsync(bool forceCapacity)
    {
        var now = resourceBudget.UtcNow;
        var candidates = GetCompletedCreations();
        var idleCandidates = new List<(AssemblyAnalysisRegistryEntryCreation, AssemblyAnalysisEntry)>();
        foreach (var creation in candidates)
        {
            var entry = await creation.Task.ConfigureAwait(false);
            if (forceCapacity
                    ? entry.IsIdleForCapacity()
                    : entry.IsIdle(now, resourceBudget.IdleTtl))
            {
                idleCandidates.Add((creation, entry));
            }
        }
        if (forceCapacity)
        {
            idleCandidates.Sort(static (left, right) =>
            {
                var comparison = left.Item2.LastUsedUtc.CompareTo(right.Item2.LastUsedUtc);
                return comparison != 0
                    ? comparison
                    : string.Compare(
                        left.Item2.CanonicalPath,
                        right.Item2.CanonicalPath,
                        StringComparison.OrdinalIgnoreCase);
            });
        }

        return idleCandidates;
    }

    private AssemblyAnalysisRegistryEntryCreation[] GetCompletedCreations()
    {
        lock (gate)
        {
            if (isDisposed()) return [];
            return entries.Values
                .Where(creation => creation.Task.IsCompletedSuccessfully)
                .ToArray();
        }
    }

    private async Task<int> RetireIdleCandidatesAsync(
        IEnumerable<(AssemblyAnalysisRegistryEntryCreation Creation, AssemblyAnalysisEntry Entry)> candidates,
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken)
    {
        var retiredCount = 0;
        foreach (var (creation, entry) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (beforeRetirementAsync is not null) await beforeRetirementAsync(entry).ConfigureAwait(false);
            if (!TryRemoveEntryForRetirement(creation, entry, out var retirement)) continue;

            await retirement!.ConfigureAwait(false);
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

    private bool TryRemoveEntryForRetirement(
        AssemblyAnalysisRegistryEntryCreation creation,
        AssemblyAnalysisEntry entry,
        out Task? retirement)
    {
        lock (gate)
        {
            var key = entries.FirstOrDefault(pair => ReferenceEquals(pair.Value, creation)).Key;
            if (key is null || !ReferenceEquals(entries[key], creation)
                || !entry.TryBeginRetirement() || !entries.Remove(key))
            {
                retirement = null;
                return false;
            }

            retirement = retireEntryAsync(creation);
            retiredEntries.Add(retirement);
            return true;
        }
    }
}
