#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Gemeinsame Union-Find- und Sortierlogik für token- und struktur-basierte Duplikat-Scans.
/// Die beiden Scanner liefern unterschiedliche Member-Metadaten, teilen aber exakt dieselbe
/// Cluster-Semantik.
/// </summary>
internal static class DuplicateClusterBuilder
{
    internal static List<DuplicateCluster> Build<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<DuplicateClusterEdge> edges,
        DuplicateDetectionOptions options,
        Func<T, int, DuplicateClusterMember> memberFactory)
    {
        var unionFind = new DuplicateUnionFind(items.Count);
        foreach (var edge in edges)
        {
            unionFind.Union(edge.A, edge.B);
        }

        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < items.Count; i++)
        {
            var root = unionFind.Find(i);
            if (!groups.TryGetValue(root, out var members))
            {
                members = [];
                groups[root] = members;
            }

            members.Add(i);
        }

        var edgesByRoot = edges.ToLookup(edge => unionFind.Find(edge.A));
        return groups.Values
            .Where(members => members.Count >= 2)
            .Select(members => BuildCluster(
                items,
                members,
                edgesByRoot[unionFind.Find(members[0])],
                options,
                memberFactory))
            .OrderByDescending(cluster => cluster.Score)
            .ThenBy(cluster => cluster.Members[0].FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(cluster => cluster.Members[0].LineNumber)
            .ToList();
    }

    private static DuplicateCluster BuildCluster<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<int> memberIndices,
        IEnumerable<DuplicateClusterEdge> clusterEdges,
        DuplicateDetectionOptions options,
        Func<T, int, DuplicateClusterMember> memberFactory)
    {
        var memberSet = new HashSet<int>(memberIndices);
        var minScore = clusterEdges
            .Where(edge => memberSet.Contains(edge.A) && memberSet.Contains(edge.B))
            .Select(edge => edge.Score)
            .DefaultIfEmpty(1.0)
            .Min();
        var members = memberIndices
            .Select(index => memberFactory(items[index], index))
            .OrderBy(member => member.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.LineNumber)
            .ToList();

        return new DuplicateCluster(
            members,
            minScore,
            DuplicateDetectionEngine.ClassifyBucket(minScore, options));
    }
}

internal readonly record struct DuplicateClusterEdge(int A, int B, double Score);
