#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// On-Demand-Erkennung semantisch aehnlicher Methoden anhand von Strukturprofilen und
/// Cosine-Similarity. Liefert dieselben Cluster-Records wie die Clone-Erkennung, erzeugt aber
/// keine Lint-Violations.
/// </summary>
internal static class StructuralDuplicateDetector
{
    internal static async Task<DuplicateDetectionScanResult> ScanAsync(
        Solution solution, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var eligible = await DuplicateMethodCollector.CollectAsync(solution, options, ct);
        var profiles = new MethodStructureProfile[eligible.Count];
        for (var i = 0; i < eligible.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            profiles[i] = StructureProfileExtractor.Extract(eligible[i]);
        }

        var edges = FindSimilarPairs(profiles, options, ct);
        var clusters = BuildClusters(eligible, profiles, edges, options);
        return new DuplicateDetectionScanResult(clusters, eligible.Count);
    }

    private static List<(int A, int B, double Score)> FindSimilarPairs(
        IReadOnlyList<MethodStructureProfile> profiles, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var edges = new List<(int A, int B, double Score)>();
        for (var i = 0; i < profiles.Count; i++)
        {
            for (var j = i + 1; j < profiles.Count; j++)
            {
                ct.ThrowIfCancellationRequested();
                if (!SharesDiscriminatingFeature(profiles[i].Features, profiles[j].Features)) continue;

                var score = StructureSimilarity.Cosine(profiles[i].Features, profiles[j].Features);
                if (score >= options.FuzzyThreshold) edges.Add((i, j, score));
            }
        }
        return edges;
    }

    private static bool SharesDiscriminatingFeature(
        IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b)
    {
        foreach (var key in a.Keys)
        {
            if (key.StartsWith("ret:", StringComparison.Ordinal)
                || key.StartsWith("form:", StringComparison.Ordinal)
                || key.StartsWith("cflowseq:", StringComparison.Ordinal)
                || key.StartsWith("target:", StringComparison.Ordinal))
            {
                if (b.ContainsKey(key)) return true;
            }
        }
        return false;
    }

    private static List<DuplicateCluster> BuildClusters(
        IReadOnlyList<EligibleMethod> methods,
        IReadOnlyList<MethodStructureProfile> profiles,
        IReadOnlyList<(int A, int B, double Score)> edges,
        DuplicateDetectionOptions options)
    {
        var unionFind = new DuplicateUnionFind(methods.Count);
        foreach (var (a, b, _) in edges) unionFind.Union(a, b);

        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < methods.Count; i++)
        {
            var root = unionFind.Find(i);
            if (!groups.TryGetValue(root, out var members))
            {
                members = [];
                groups[root] = members;
            }
            members.Add(i);
        }

        var edgesByRoot = edges.ToLookup(e => unionFind.Find(e.A));
        return groups.Values
            .Where(members => members.Count >= 2)
            .Select(members => BuildCluster(methods, profiles, members, edgesByRoot[unionFind.Find(members[0])], options))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Members[0].FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Members[0].LineNumber)
            .ToList();
    }

    private static DuplicateCluster BuildCluster(
        IReadOnlyList<EligibleMethod> methods,
        IReadOnlyList<MethodStructureProfile> profiles,
        IReadOnlyList<int> memberIndices,
        IEnumerable<(int A, int B, double Score)> clusterEdges,
        DuplicateDetectionOptions options)
    {
        var memberSet = new HashSet<int>(memberIndices);
        var minScore = clusterEdges
            .Where(e => memberSet.Contains(e.A) && memberSet.Contains(e.B))
            .Select(e => e.Score)
            .DefaultIfEmpty(1.0)
            .Min();

        var members = memberIndices
            .OrderBy(i => methods[i].FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => methods[i].LineNumber)
            .Select(i => new DuplicateClusterMember(
                methods[i].FilePath,
                methods[i].LineNumber,
                methods[i].SignatureName,
                methods[i].TokenCount,
                profiles[i].Summary))
            .ToList();

        return new DuplicateCluster(members, minScore, DuplicateDetectionEngine.ClassifyBucket(minScore, options));
    }
}
