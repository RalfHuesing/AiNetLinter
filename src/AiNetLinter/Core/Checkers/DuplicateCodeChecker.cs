#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Models;
using AiNetLinter.Suppression;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Solution-weite Duplicate-Code-Pruefung (Nachpruefung auf dem aggregierten
/// <see cref="AnalysisState"/>, analog <see cref="UiFileSeparationChecker"/> — anders als die
/// meisten Checker in diesem Ordner (Roslyn-Syntax-Node-Walker pro Datei, z. B.
/// <c>AsyncVoidChecker.CheckMethod</c>) braucht Duplicate-Detection eine solution-weite Sicht
/// auf alle Methoden gleichzeitig, siehe <see cref="PostAnalysisChecks.RunAsync"/>). Nutzt die
/// gemeinsame <see cref="DuplicateDetectionEngine"/> auf <see cref="AnalysisState.Solution"/>.
/// <para/>
/// Nur <c>exact</c>-Cluster werden gemeldet (nicht <c>near</c>/<c>fuzzy</c> — siehe Live-Dogfood-
/// Befund 2026-08-11: auf diesem Repo selbst produzierten <c>near</c>-Cluster allein ~23
/// Einzel-Funde, was das False-Positive-Budget aus <c>07-drift-audit-ideen.md</c> §A.7 fuer
/// automatisches Lint sprengt — <c>near</c>/<c>fuzzy</c> bleiben ueber <c>find_duplicates</c>/den
/// Drift-Audit-Skill weiterhin voll einsehbar, nur eben nicht als automatischer Lint-Verstoss).
/// <para/>
/// Genau eine <see cref="RuleViolation"/> pro Cluster (repraesentatives erstes Mitglied nach
/// Datei/Zeile, analog <see cref="PostAnalysisChecks.RunMaxPartialClassFilesCheck"/>s
/// "representative"-Muster) statt einer pro Cluster-Mitglied — ein Duplikat-Fund ist EIN Befund,
/// keine <c>N</c> unabhaengigen Befunde; <see cref="RuleViolation.Details"/> listet trotzdem alle
/// Mitglieder vollstaendig.
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
            .Where(c => c.Bucket is DuplicateSimilarityBucket.Exact)
            .Where(c => !IsClusterSuppressed(c, state.FileContents))
            .Take(global.DuplicateCodeMaxResults);

        foreach (var cluster in reportable)
        {
            state.Violations.Add(BuildViolation(cluster));
        }
    }

    /// <summary>
    /// Prueft <c>// ainetlinter-disable DuplicateCode</c> (dieselbe dateiweite Konvention wie bei
    /// jedem anderen Checker, siehe <see cref="SuppressionEvaluator"/>) ueber ALLE Cluster-Mitglieder,
    /// nicht nur das repraesentative — wer den Fund fuer eine seiner eigenen Methoden begruenden will
    /// (z. B. "strukturell gleich, aber fachlich unterschiedlich"), setzt den Kommentar in der Datei,
    /// die er gerade bearbeitet, unabhaengig davon, welches Mitglied zufaellig als Repraesentant
    /// gewaehlt wurde (siehe <see cref="BuildViolation"/>: alphabetisch erstes Mitglied). Ein
    /// Suppress-Kommentar in irgendeiner beteiligten Datei reicht, um den gesamten Cluster-Fund zu
    /// unterdruecken — der Fund ist EINE Aussage ueber die Beziehung zwischen den Methoden, keine
    /// pro Datei unabhaengige.
    /// </summary>
    private static bool IsClusterSuppressed(DuplicateCluster cluster, ConcurrentDictionary<string, string> fileContents) =>
        cluster.Members.Any(m => IsSuppressed(m.FilePath, fileContents));

    private static bool IsSuppressed(string filePath, ConcurrentDictionary<string, string> fileContents)
    {
        var content = fileContents.GetOrAdd(filePath, fp => File.Exists(fp) ? File.ReadAllText(fp) : string.Empty);
        return SuppressionEvaluator.IsSuppressed(content, LinterRuleIds.DuplicateCode, lineNumber: 0);
    }

    private static RuleViolation BuildViolation(DuplicateCluster cluster)
    {
        var representative = cluster.Members[0];
        return new RuleViolation
        {
            FilePath = representative.FilePath,
            LineNumber = representative.LineNumber,
            RuleName = LinterRuleIds.DuplicateCode,
            Details = BuildDetails(cluster),
            Guidance = BuildGuidance(cluster.Members),
        };
    }

    private static string BuildDetails(DuplicateCluster cluster)
    {
        var sb = new StringBuilder();
        sb.Append($"{cluster.Members.Count} Methoden sind exact (Jaccard-Score {cluster.Score:F2}) zueinander:");
        foreach (var member in cluster.Members)
        {
            sb.Append($"\n  + {member.SignatureName} ({System.IO.Path.GetFileName(member.FilePath)}:{member.LineNumber})");
        }
        return sb.ToString();
    }

    private static string BuildGuidance(IReadOnlyList<DuplicateClusterMember> members)
    {
        var names = string.Join(", ", members.Select(m => m.SignatureName));
        return $"Extrahiere die gemeinsame Logik in eine wiederverwendbare Methode/Klasse und rufe sie " +
               $"von allen beteiligten Stellen auf ({names}), statt sie mehrfach zu duplizieren. Falls die " +
               "Aehnlichkeit beabsichtigt ist (z. B. strukturell gleiche, aber fachlich unterschiedliche " +
               "Methoden): '// ainetlinter-disable DuplicateCode' in einer der beteiligten Dateien " +
               "platzieren (idealerweise mit kurzer Begruendung in derselben Zeile/direkt darueber) " +
               "statt die Regel global in rules.json ueber 'EnableDuplicateCodeCheck' zu deaktivieren.";
    }
}
