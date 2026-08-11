#nullable enable

using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Solution-weite Duplicate-Code-Pruefung (Nachpruefung auf dem aggregierten
/// <see cref="AnalysisState"/>, analog <see cref="UiFileSeparationChecker"/> — anders als die
/// meisten Checker in diesem Ordner (Roslyn-Syntax-Node-Walker pro Datei, z. B.
/// <c>AsyncVoidChecker.CheckMethod</c>) braucht Duplicate-Detection eine solution-weite Sicht
/// auf alle Methoden gleichzeitig, siehe <see cref="PostAnalysisChecks.RunAsync"/>). Nutzt die
/// gemeinsame <see cref="DuplicateDetectionEngine"/> auf <see cref="AnalysisState.Solution"/> und
/// meldet fuer jedes <c>exact</c>- und <c>near</c>-Cluster (nicht <c>fuzzy</c> — das waere zu viel
/// Rauschen fuer automatisches Lint, siehe <c>tasks/features/07-drift-audit-ideen.md</c> §A.4)
/// eine <see cref="RuleViolation"/> pro beteiligter Methode.
/// </summary>
internal static class DuplicateCodeChecker
{
    internal static async Task RunAsync(AnalysisState state, Config config, CancellationToken ct = default)
    {
        var global = config.Global;
        if (!global.EnableDuplicateCodeCheck) return;

        var options = new DuplicateDetectionOptions(
            MinTokens: global.DuplicateCodeMinTokens,
            NgramSize: global.DuplicateCodeNgramSize,
            MinSharedNgrams: global.DuplicateCodeMinSharedNgrams,
            ExactThreshold: global.DuplicateCodeExactThreshold,
            NearThreshold: global.DuplicateCodeNearThreshold,
            FuzzyThreshold: global.DuplicateCodeFuzzyThreshold,
            NormalizeIdentifiers: global.DuplicateCodeNormalizeIdentifiers);

        var result = await DuplicateDetectionEngine.ScanAsync(state.Solution, options, ct).ConfigureAwait(false);

        var reportable = result.Clusters
            .Where(c => c.Bucket is DuplicateSimilarityBucket.Exact or DuplicateSimilarityBucket.Near)
            .Take(global.DuplicateCodeMaxResults);

        foreach (var cluster in reportable)
        {
            AddViolationsForCluster(cluster, state.Violations);
        }
    }

    private static void AddViolationsForCluster(
        DuplicateCluster cluster, System.Collections.Concurrent.ConcurrentBag<RuleViolation> violations)
    {
        var bucketLabel = cluster.Bucket == DuplicateSimilarityBucket.Exact ? "exact" : "near";
        foreach (var member in cluster.Members)
        {
            var others = cluster.Members.Where(m => m != member).ToList();
            violations.Add(new RuleViolation
            {
                FilePath = member.FilePath,
                LineNumber = member.LineNumber,
                RuleName = LinterRuleIds.DuplicateCode,
                Details = BuildDetails(member, others, cluster.Score, bucketLabel),
                Guidance = BuildGuidance(others),
            });
        }
    }

    private static string BuildDetails(
        DuplicateClusterMember member,
        System.Collections.Generic.IReadOnlyList<DuplicateClusterMember> others,
        double score,
        string bucketLabel)
    {
        var sb = new StringBuilder();
        sb.Append($"'{member.SignatureName}' ist {bucketLabel} (Jaccard-Score {score:F2}) zu {others.Count} weiteren Methode(n) im Cluster:");
        foreach (var other in others)
        {
            sb.Append($"\n  + {other.SignatureName} ({System.IO.Path.GetFileName(other.FilePath)}:{other.LineNumber})");
        }
        return sb.ToString();
    }

    private static string BuildGuidance(System.Collections.Generic.IReadOnlyList<DuplicateClusterMember> others)
    {
        var otherNames = string.Join(", ", others.Select(o => o.SignatureName));
        return $"Extrahiere die gemeinsame Logik in eine wiederverwendbare Methode/Klasse und rufe sie " +
               $"von allen beteiligten Stellen auf (dieser Methode und {otherNames}), statt sie mehrfach " +
               "zu duplizieren. Falls die Aehnlichkeit beabsichtigt ist (z. B. strukturell gleiche, aber " +
               "fachlich unterschiedliche Methoden), Regel gezielt unterdruecken statt global zu deaktivieren.";
    }
}
