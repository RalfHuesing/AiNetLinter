#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core.DuplicateDetection;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Adapter fuer <c>find_duplicates mode=structural</c>: loest Argumente zu Cosine-Schwellwerten
/// aus <see cref="GlobalConfig"/> auf (getrennt von den Jaccard-<c>DuplicateCode*</c>-Werten),
/// ruft <see cref="StructuralDuplicateDetector"/> und kappt auf <c>maxResults</c>.
/// </summary>
internal static class StructuralDuplicateScanner
{
    internal static async Task<DuplicateDetectionScanResultForTool> ScanAsync(
        Solution solution, GlobalConfig config, DuplicateDetectionInput input, DuplicateSimilarityBucket minBucket,
        CancellationToken ct)
    {
        var options = BuildOptions(config, input);
        var scanResult = await StructuralDuplicateDetector.ScanAsync(solution, options, ct);

        var filtered = scanResult.Clusters.Where(c => c.Bucket >= minBucket).ToList();
        var effectiveMax = Math.Max(1, input.MaxResults ?? config.DuplicateCodeMaxResults);
        var shown = filtered.Count <= effectiveMax ? filtered : filtered.Take(effectiveMax).ToList();
        var truncated = filtered.Count > effectiveMax;

        return new DuplicateDetectionScanResultForTool(shown, filtered.Count, scanResult.MethodsScanned, truncated);
    }

    internal static DuplicateDetectionOptions BuildOptions(GlobalConfig config, DuplicateDetectionInput input) =>
        new(
            MinTokens: input.MinTokens ?? config.DuplicateCodeMinTokens,
            NgramSize: config.DuplicateCodeNgramSize,
            MinSharedNgrams: config.DuplicateCodeMinSharedNgrams,
            ExactThreshold: config.StructuralDuplicateExactThreshold,
            NearThreshold: config.StructuralDuplicateNearThreshold,
            FuzzyThreshold: config.StructuralDuplicateFuzzyThreshold,
            NormalizeIdentifiers: false,
            PathScopeFilter: string.IsNullOrWhiteSpace(input.ScopeDir) ? null : Output.PathNormalizer.NormalizeSeparators(input.ScopeDir),
            ScopeType: string.IsNullOrWhiteSpace(input.ScopeType) ? "all" : input.ScopeType.Trim().ToLowerInvariant());
}
