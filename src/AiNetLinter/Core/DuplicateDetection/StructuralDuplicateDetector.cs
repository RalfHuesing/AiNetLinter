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
        var clusters = DuplicateClusterBuilder.Build(
            eligible,
            edges,
            options,
            (method, index) => new DuplicateClusterMember(
                method.FilePath,
                method.LineNumber,
                method.SignatureName,
                method.TokenCount,
                profiles[index].Summary));
        return new DuplicateDetectionScanResult(clusters, eligible.Count);
    }

    private static List<DuplicateClusterEdge> FindSimilarPairs(
        IReadOnlyList<MethodStructureProfile> profiles, DuplicateDetectionOptions options, CancellationToken ct)
    {
        var edges = new List<DuplicateClusterEdge>();
        for (var i = 0; i < profiles.Count; i++)
        {
            for (var j = i + 1; j < profiles.Count; j++)
            {
                ct.ThrowIfCancellationRequested();
                if (!SharesDiscriminatingFeature(profiles[i].Features, profiles[j].Features)) continue;

                var score = StructureSimilarity.Cosine(profiles[i].Features, profiles[j].Features);
                if (score >= options.FuzzyThreshold) edges.Add(new DuplicateClusterEdge(i, j, score));
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

}
