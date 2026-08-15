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
/// Duenner Adapter zwischen <c>find_duplicates</c> und der gemeinsamen
/// <see cref="DuplicateDetectionEngine"/> (Core/DuplicateDetection/, geteilt mit
/// <c>DuplicateCodeChecker</c>) — loest Tool-Argumente + <see cref="GlobalConfig"/>-Defaults zu
/// <see cref="DuplicateDetectionOptions"/> auf, ruft die Engine, filtert das Ergebnis auf den
/// gewuenschten <see cref="DuplicateSimilarityBucket"/>-Mindestwert und kappt auf
/// <c>maxResults</c>. Keine Text-/JSON-Formatierung hier (macht <see cref="DuplicateDetectionTool"/>),
/// analog <c>DependencyGraphScanner</c>/<c>DependencyGraphTool</c>.
/// </summary>
internal static class DuplicateDetectionScanner
{
    internal static async Task<DuplicateDetectionScanResultForTool> ScanAsync(
        Solution solution, GlobalConfig config, DuplicateDetectionInput input, DuplicateSimilarityBucket minBucket,
        CancellationToken ct)
    {
        var options = BuildOptions(config, input);
        var scanResult = await DuplicateDetectionEngine.ScanAsync(solution, options, ct);

        var filtered = scanResult.Clusters.Where(c => c.Bucket >= minBucket).ToList();
        var effectiveMax = Math.Max(1, input.MaxResults ?? config.DuplicateCodeMaxResults);
        var shown = filtered.Count <= effectiveMax ? filtered : filtered.Take(effectiveMax).ToList();
        var truncated = filtered.Count > effectiveMax;

        return new DuplicateDetectionScanResultForTool(shown, filtered.Count, scanResult.MethodsScanned, truncated);
    }

    /// <summary>Loest Tool-Argumente + <see cref="GlobalConfig"/>-Defaults zu
    /// <see cref="DuplicateDetectionOptions"/> auf — von <see cref="ScanAsync"/> (Teil A,
    /// <c>mode="clone"</c>) UND <see cref="RefactoringDriftScanner.ScanAsync"/> (Teil C,
    /// <c>mode="refactoring-drift"</c>) gemeinsam genutzt, damit die Argument-Aufloesungs-Regeln
    /// (Tool-Argument ueberschreibt Config-Default) nicht zweimal gepflegt werden.</summary>
    internal static DuplicateDetectionOptions BuildOptions(GlobalConfig config, DuplicateDetectionInput input) =>
        new(
            MinTokens: input.MinTokens ?? config.DuplicateCodeMinTokens,
            NgramSize: config.DuplicateCodeNgramSize,
            MinSharedNgrams: config.DuplicateCodeMinSharedNgrams,
            ExactThreshold: config.DuplicateCodeExactThreshold,
            NearThreshold: config.DuplicateCodeNearThreshold,
            FuzzyThreshold: config.DuplicateCodeFuzzyThreshold,
            NormalizeIdentifiers: input.NormalizeIdentifiers ?? config.DuplicateCodeNormalizeIdentifiers,
            PathScopeFilter: string.IsNullOrWhiteSpace(input.ScopeDir) ? null : Output.PathNormalizer.NormalizeSeparators(input.ScopeDir),
            ScopeType: string.IsNullOrWhiteSpace(input.ScopeType) ? "all" : input.ScopeType.Trim().ToLowerInvariant());
}
