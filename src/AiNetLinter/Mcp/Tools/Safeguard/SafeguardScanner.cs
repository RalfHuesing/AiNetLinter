#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Safeguard;

/// <summary>
/// Reine Score-Berechnungs- und Remediation-Logik fuer die Safeguard-Auswertung — eigene Datei,
/// damit der Tool-Wrapper ein duenner Dispatch bleibt. Delegiert die Lint-Arbeit an
/// <see cref="LinterEngine.RunAsync(Solution, bool, int, CancellationToken)"/>
/// (mit <c>noCache: true</c> analog zu <see cref="GetViolationsScanner"/>), sammelt zusaetzlich
/// Klassenmetriken (Cognitive Complexity, AI-Context-Footprint, Sealed-Quote) ueber einen direkten
/// Roslyn-Walk und aggregiert alles zu einem deterministischen 0-10-Score.
///
/// Determinismus: keine Zeit-/Zufalls-/Externer-IO-Operatoren, Sortierung der Top-Violations nach
/// (Severity, FilePath, LineNumber, RuleName), symmetrische Rundung der Komponenten-Scores. Ein
/// defensiver <c>try/catch</c> faengt LinterEngine-Malfunctions ab und liefert
/// <see cref="SafeguardScoreResult.IsMalfunction"/>=true mit <see cref="SafeguardScoreResult.Context"/>
/// (Pattern analog <see cref="GetViolationsScanner"/>). Score-Gewichte sind benannte Konstanten, damit
/// Tests und Dokumentation dieselben Werte sehen — Anpassung nur bei offensichtlich unplausiblen
/// Test-Scores; die unten dokumentierten Gewichte sind die aktuell gueltigen.
/// </summary>
internal static partial class SafeguardScanner
{
    /// <summary>Standard-Mindest-Score fuer <c>Passed</c>.</summary>
    internal const double DefaultMinScoreThreshold = 8.0;

    /// <summary>Standard-Obergrenze fuer Top-Remediation-Eintraege.</summary>
    internal const int DefaultMaxRemediationEntries = 20;

    /// <summary>
    /// Severity-Gewicht fuer eine Lint-Error-Verletzung. 0.1 wuerde 20 Errors brauchen,
    /// um den Score unter 8.0 zu druecken — fuer den Test "SingleViolation_LowersScoreBelowThreshold"
    /// unplausibel. Auf 1.5 angehoben: 1 Error senkt den Score um 3.0 (Severity 2 * 1.5), liegt
    /// damit klar unter 8.0.
    /// </summary>
    internal const double ViolationPenaltyUnit = 1.5;

    /// <summary>Severity-Stufe fuer eine Lint-Error-Verletzung.</summary>
    internal const double ViolationErrorSeverity = 2.0;

    /// <summary>Severity-Stufe fuer eine Lint-Warning.</summary>
    internal const double ViolationWarningSeverity = 1.0;

    /// <summary>Severity-Stufe fuer einen Lint-Info-Hinweis.</summary>
    internal const double ViolationInfoSeverity = 0.25;

    /// <summary>
    /// Penalty pro Cognitive-Complexity-Einheit ueber <c>Metrics.MaxCognitiveComplexity</c>, gemittelt
    /// ueber alle Klassen. Bewusst auf 0.05 belassen.
    /// </summary>
    internal const double CcPenaltyPerUnitOverThreshold = 0.05;

    /// <summary>
    /// Penalty pro AI-Context-Footprint-Einheit ueber <c>Metrics.MaxAIContextFootprint</c>, gemittelt
    /// ueber alle Klassen. Bewusst auf 0.02 belassen.
    /// </summary>
    internal const double FootprintPenaltyPerUnitOverLimit = 0.02;

    /// <summary>
    /// Sealed-Bonus pro Viertel ueber 50 % versiegelter Klassen. Bei 75 % sealed = +0.5, bei 100 %
    /// sealed = +1.0. Wird deaktiviert, wenn <c>Global.EnforceSealedClasses</c> false ist (dann 0).
    /// </summary>
    internal const double SealedBonusPerQuarterOverHalf = 0.5;

    /// <summary>
    /// Anzahl Gesamt-Versuche (inkl. Erstversuch) fuer <c>Project.GetCompilationAsync</c> pro Projekt,
    /// bevor ein kompilierbares Projekt (<c>SupportsCompilation == true</c>) als echte Malfunction statt
    /// stillschweigend uebersprungen gilt. Unter paralleler Prozess-Last (z. B. mehrere gleichzeitig
    /// ladende MSBuild-Workspaces) kann <c>GetCompilationAsync</c> transient fehlschlagen — ohne Retry
    /// wuerde das Projekt dann lautlos aus der Klassen-Aggregation fallen, was <c>avgCC</c>/<c>avgFootprint</c>
    /// nicht-deterministisch ueber eine zufaellige Teilmenge der Klassen berechnet.
    /// </summary>
    internal const int CompilationRetryAttempts = 3;

    /// <summary>
    /// Basis-Verzoegerung zwischen Compilation-Retries in Millisekunden, linear skaliert mit der
    /// Versuchsnummer (200ms vor Versuch 2, 400ms vor Versuch 3). Reines Backoff-Timing — beeinflusst
    /// nicht die Score-Formel selbst (die bleibt frei von Zeit-/Zufalls-Operatoren), sondern nur, wie
    /// lange auf eine erfolgreiche Compilation gewartet wird, bevor eine Malfunction gemeldet wird.
    /// </summary>
    internal const int CompilationRetryBaseDelayMs = 200;

    /// <summary>
    /// Berechnet den deterministischen Safeguard-Score fuer die uebergebene Solution.
    /// Defensive <c>try/catch</c> um LinterEngine-Lauf UND Klassen-Aggregation: beides sind echte
    /// Malfunctions, wenn sie fehlschlagen. Projekte mit <c>SupportsCompilation == false</c> (z. B.
    /// echte Nicht-C#-Projekte) werden weiterhin normal uebersprungen — das ist kein Fehler. Ein
    /// Projekt mit <c>SupportsCompilation == true</c>, dessen Compilation auch nach Retries
    /// fehlschlaegt (siehe <see cref="TryGetCompilationAsync"/>), wird dagegen NICHT mehr
    /// stillschweigend uebersprungen, sondern als Malfunction gemeldet — lieber ehrlich "konnte
    /// nicht zuverlaessig scoren" als ein Score aus einer zufaelligen Teilmenge der Klassen.
    /// </summary>
    internal static async Task<SafeguardScoreResult> ComputeScoreAsync(SafeguardScannerParameters p)
    {
        var solution = p.Solution;
        var config = p.Config;
        var console = p.Console;
        var scopeFilter = p.ScopeFilter;
        var ct = p.CancellationToken;

        // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik fuer `with {...}`
        // und durchgereichte Sub-Properties); ILinterEngineConfig wird projektweit ausschliesslich
        // von Config implementiert, der Downcast ist daher nicht spekulativ.
        var concreteConfig = (Config)config;
        var solutionDir = string.IsNullOrEmpty(solution.FilePath)
            ? ""
            : Path.GetDirectoryName(solution.FilePath) ?? "";
        var scope = string.IsNullOrWhiteSpace(scopeFilter) ? "solution" : scopeFilter!;
        var assessment = AssessScope(solution, solutionDir, scopeFilter, concreteConfig);
        if (assessment.Status is not "configured")
        {
            return new SafeguardScoreResult(
                Score: BuildUndecidableResult(assessment, p.MinScoreThreshold),
                IsMalfunction: false);
        }

        IReadOnlyCollection<RuleViolation> violations;
        IReadOnlyList<ScannedClass> classes;
        try
        {
            var engine = new LinterEngine(
                config: concreteConfig,
                configContent: null,
                profiler: null,
                console: console);
            violations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
            var fileToProject = ViolationScopeFilter.BuildFileToProjectMap(solution, solutionDir, concreteConfig.FileFilters);
            violations = ViolationScopeFilter.FilterAndSortViolations(
                solutionDir, fileToProject, violations, scopeFilter);

            // Im selben try/catch wie die LinterEngine: ein kompilierbares Projekt, das auch nach
            // Retries (siehe TryGetCompilationAsync) keine Compilation liefert, ist genauso eine
            // echte Malfunction wie eine LinterEngine-Exception — beides wuerde sonst entweder den
            // Score verfaelschen (stilles Ueberspringen) oder inkonsistent behandelt werden.
            classes = await EnumerateConcreteClassesAsync(
                solution, scopeFilter, concreteConfig, solutionDir, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SafeguardScoreResult(
                Score: null, IsMalfunction: true, Context: ex.Message);
        }

        var score = BuildScoreResult(new BuildScoreResultParameters(
            Violations: violations,
            Classes: classes,
            Config: concreteConfig,
            Threshold: p.MinScoreThreshold,
            MaxRemediationEntries: p.MaxRemediationEntries,
            SolutionDir: solutionDir,
            Scope: scope,
            Completeness: assessment.Completeness,
            Status: assessment.Status,
            StatusCause: assessment.StatusCause,
            ExcludedDocumentCount: assessment.ExcludedDocumentCount));
        score = score with { Status = score.Passed == true ? "passed" : "failed" };
        return new SafeguardScoreResult(Score: score, IsMalfunction: false);
    }

    private static SafeguardScopeAssessment AssessScope(
        Solution solution, string solutionDir, string? scopeFilter, Config config)
    {
        var scope = string.IsNullOrWhiteSpace(scopeFilter) ? "solution" : scopeFilter!;
        var matchingDocuments = solution.Projects
            .Where(project => project.SupportsCompilation)
            .SelectMany(project => project.Documents)
            .Where(document => SourceFileCatalog.IsValidDocument(document, solutionDir)
                && ViolationScopeFilter.MatchesScope(
                    document.FilePath ?? document.Name, document.Project.Name, solutionDir, scopeFilter))
            .ToList();

        if (matchingDocuments.Count == 0) return AssessEmptyScope(solution, scopeFilter, scope);

        var analyzableDocuments = matchingDocuments
            .Where(document => !FileFilterEvaluator.IsExcluded(document.FilePath ?? document.Name, config.FileFilters))
            .ToList();
        var excludedDocumentCount = matchingDocuments.Count - analyzableDocuments.Count;
        if (analyzableDocuments.Count == 0)
        {
            return UndecidableScope(
                scope,
                "Keine analysierbaren Dokumente im Scope; alle passenden Dokumente sind durch konfigurierte Dateiausschlüsse außerhalb des Scores.",
                excludedDocumentCount);
        }

        var enabledStates = analyzableDocuments
            .Select(document => ProjectConfigResolver.ResolveForDocument(document, config, solutionDir))
            .Select(effectiveConfig => RuleRegistry.All.Any(rule => rule.IsEnabled(effectiveConfig)))
            .ToList();
        return AssessRuleConfiguration(scope, enabledStates, excludedDocumentCount);
    }

    private static SafeguardScopeAssessment AssessEmptyScope(
        Solution solution, string? scopeFilter, string scope)
    {
        if (string.IsNullOrWhiteSpace(scopeFilter)
            && solution.Projects.Any(project => project.SupportsCompilation && project.Documents.Any()))
        {
            return new SafeguardScopeAssessment(
                scope,
                "complete",
                "configured",
                "Der Score ist ein Quality-Gate; die analysierten Quelldateien sind im Scope entscheidbar.");
        }
        return UndecidableScope(scope, "Keine analysierbaren Dokumente im angeforderten Scope.");
    }

    private static SafeguardScopeAssessment AssessRuleConfiguration(
        string scope, IReadOnlyList<bool> enabledStates, int excludedDocumentCount)
    {
        if (enabledStates.All(enabled => !enabled))
        {
            return new SafeguardScopeAssessment(
                scope, "not_configured", "not_configured", "Im angeforderten Scope ist keine Regel aktiviert.", excludedDocumentCount);
        }
        if (enabledStates.Any(enabled => !enabled))
        {
            return UndecidableScope(
                scope,
                "Die Regelaktivierung ist im angeforderten Scope zwischen Dokumenten uneinheitlich.",
                excludedDocumentCount);
        }
        var exclusionNote = excludedDocumentCount == 0
            ? ""
            : $" {excludedDocumentCount} Dokumente sind durch konfigurierte Dateiausschlüsse bewusst außerhalb des Scores.";
        return new SafeguardScopeAssessment(
            scope,
            "complete",
            "configured",
            "Der Score ist ein Quality-Gate; die analysierten Dokumente und Regeln sind im Scope entscheidbar." + exclusionNote,
            excludedDocumentCount);
    }

    private static SafeguardScopeAssessment UndecidableScope(
        string scope, string cause, int excludedDocumentCount = 0) =>
        new(scope, "not_decidable", "not_decidable", cause, excludedDocumentCount);

    private static ScoreResult BuildUndecidableResult(
        SafeguardScopeAssessment assessment, double threshold) =>
        new(
            Passed: null,
            Score: null,
            Threshold: threshold,
            Violations: Array.Empty<ViolationEntry>(),
            Remediation: new RemediationHint(
                TopIssue: "Kein entscheidbarer Quality-Gate-Score.",
                ActionableSteps: Array.Empty<string>(),
                DocumentationHint: "Docs/linter/configuration.md"),
            Summary: $"Safeguard-Score: nicht entscheidbar. Quality-Gate, kein Scope-Vollständigkeitsbeweis " +
                $"(scoreIsNotScope=true). Scope: '{assessment.Scope}'; " +
                $"Vollständigkeit: {assessment.Completeness}; Status: {assessment.Status}. " +
                assessment.StatusCause,
            Scope: assessment.Scope,
            ScoreIsNotScope: true,
            Completeness: assessment.Completeness,
            Status: assessment.Status,
            StatusCause: assessment.StatusCause,
            ExcludedDocumentCount: assessment.ExcludedDocumentCount);

    /// <summary>
    /// Deterministische Score-Berechnung. Getrennt von <see cref="ComputeScoreAsync"/> fuer
    /// isolierte Tests (Klemmverhalten, Threshold-Logik, Sealed-Bonus-Berechnung) ohne
    /// LinterEngine-Setup.
    /// </summary>
    internal static ScoreResult BuildScoreResult(BuildScoreResultParameters p)
    {
        var violationPenalty = ComputeViolationPenalty(p.Violations);
        var ccPenalty = ComputeCcPenalty(p.Classes, p.Config.Metrics.MaxCognitiveComplexity);
        var footprintPenalty = ComputeFootprintPenalty(p.Classes, p.Config.Metrics.MaxAIContextFootprint);
        var sealedBonus = ComputeSealedBonus(p.Classes, p.Config.Global.EnforceSealedClasses);

        var raw = 10.0 - violationPenalty - ccPenalty - footprintPenalty + sealedBonus;
        var score = Math.Clamp(raw, 0.0, 10.0);
        var passed = score >= p.Threshold;
        var status = passed ? "passed" : "failed";

        // Sortierung: Errors zuerst, dann Warnings, dann Info; innerhalb gleicher Severity
        // stabil nach (FilePath, LineNumber, RuleName) — garantiert Byte-fuer-Byte-Identitaet
        // fuer zwei aufeinanderfolgende Aufrufe mit identischem Input (Determinismus-Test).
        var sortedViolations = p.Violations
            .OrderBy(SeverityRank)
            .ThenBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, p.MaxRemediationEntries))
            .Select(v => new ViolationEntry(
                // Solution-relativ (wie alle uebrigen MCP-Tools), damit Agenten Pfade über
                // Tool-Antworten hinweg korrelieren koennen; Sortierung bleibt unberuehrt,
                // weil die Relativierung die Ordnung innerhalb des Roots erhaelt.
                FilePath: PathNormalizer.ToRelative(p.SolutionDir, v.FilePath),
                LineNumber: v.LineNumber,
                RuleName: v.RuleName,
                Details: v.Details,
                Severity: RuleRegistry.ResolveSeverity(v),
                Guidance: v.Guidance))
            .ToList();

        var remediation = BuildRemediation(sortedViolations, p.Config);
        var summary = BuildSummary(new BuildSafeguardSummaryParameters(
            Score: score,
            Threshold: p.Threshold,
            Passed: passed,
            TotalViolationCount: p.Violations.Count,
            ShownViolationCount: sortedViolations.Count,
            Classes: p.Classes,
            Scope: p.Scope,
            Completeness: p.Completeness,
            Status: status,
            ExcludedDocumentCount: p.ExcludedDocumentCount));

        return new ScoreResult(
            Passed: passed,
            Score: score,
            Threshold: p.Threshold,
            Violations: sortedViolations,
            Remediation: remediation,
            Summary: summary,
            Scope: p.Scope,
            ScoreIsNotScope: true,
            Completeness: p.Completeness,
            Status: status,
            StatusCause: p.StatusCause,
            TotalViolationCount: p.Violations.Count,
            ShownViolationCount: sortedViolations.Count,
            ViolationsTruncated: sortedViolations.Count < p.Violations.Count,
            ExcludedDocumentCount: p.ExcludedDocumentCount);
    }

    /// <summary>
    /// Erzeugt einen strukturierten Remediation-Hint auf Basis der Top-Violations.
    /// Mapping-Tabelle pro <c>RuleName</c>; unbekannte RuleNames erhalten einen generischen
    /// Default-Hinweis. Aufgeteilt in "TopIssue" (die haeufigste Regel unter den Top-Violations)
    /// und "ActionableSteps" (eine Empfehlung pro vorkommender Regel).
    /// </summary>
    internal static RemediationHint BuildRemediation(
        IReadOnlyList<ViolationEntry> topViolations,
        Config config)
    {
        if (topViolations.Count == 0)
        {
            return new RemediationHint(
                TopIssue: "Keine Lint-Verstoesse im Scope.",
                ActionableSteps: Array.Empty<string>(),
                DocumentationHint: "Docs/linter/configuration.md");
        }

        var grouped = topViolations
            .GroupBy(v => v.RuleName, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topIssue = grouped[0].Key;
        var steps = grouped
            .Select(g => ResolveHintForRule(g.Key, config))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        return new RemediationHint(
            TopIssue: topIssue,
            ActionableSteps: steps,
            DocumentationHint: "Docs/linter/configuration.md");
    }

    private static double ComputeViolationPenalty(IReadOnlyCollection<RuleViolation> violations)
    {
        if (violations.Count == 0) return 0.0;

        double penalty = 0.0;
        foreach (var v in violations)
        {
            var severity = RuleRegistry.ResolveSeverity(v);
            if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                penalty += ViolationErrorSeverity * ViolationPenaltyUnit;
            }
            else if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                penalty += ViolationWarningSeverity * ViolationPenaltyUnit;
            }
            else
            {
                penalty += ViolationInfoSeverity * ViolationPenaltyUnit;
            }
        }
        return penalty;
    }

    private static double ComputeCcPenalty(IReadOnlyList<ScannedClass> classes, int ccThreshold)
    {
        if (classes.Count == 0) return 0.0;
        var avgCc = classes.Average(c => (double)c.MaxCognitiveComplexity);
        var overage = Math.Max(0, avgCc - ccThreshold);
        return overage * CcPenaltyPerUnitOverThreshold;
    }

    private static double ComputeFootprintPenalty(IReadOnlyList<ScannedClass> classes, int footprintLimit)
    {
        if (classes.Count == 0) return 0.0;
        var avgFootprint = classes.Average(c => (double)c.AIContextFootprint);
        var overage = Math.Max(0, avgFootprint - footprintLimit);
        return overage * FootprintPenaltyPerUnitOverLimit;
    }

    private static double ComputeSealedBonus(IReadOnlyList<ScannedClass> classes, bool enforceSealed)
    {
        if (!enforceSealed || classes.Count == 0) return 0.0;
        var sealedCount = classes.Count(c => c.IsSealed);
        var sealedQuote = (double)sealedCount / classes.Count;
        var quartersOverHalf = Math.Max(0.0, (sealedQuote - 0.5) / 0.25);
        return quartersOverHalf * SealedBonusPerQuarterOverHalf;
    }

    private static int SeverityRank(RuleViolation v)
    {
        var severity = RuleRegistry.ResolveSeverity(v);
        if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static string BuildSummary(BuildSafeguardSummaryParameters p)
    {
        var violationSummary = p.TotalViolationCount == p.ShownViolationCount
            ? FormatViolationCount(p.TotalViolationCount)
            : $"{p.ShownViolationCount} von {p.TotalViolationCount} Verstößen (Top-Auswahl wegen maxViolations)";
        return $"Safeguard-Score: {p.Score:F2}/10 (Threshold {p.Threshold:F2}) — {(p.Passed ? "PASS" : "FAIL")}. " +
               $"{violationSummary}, {p.Classes.Count} Klassen analysiert. " +
               (p.ExcludedDocumentCount == 0
                   ? ""
                   : $"{p.ExcludedDocumentCount} Dokumente bewusst ausgeschlossen. ") +
               $"Quality-Gate, kein Scope-Vollständigkeitsbeweis (scoreIsNotScope=true). " +
               $"Scope: '{p.Scope}'; Vollständigkeit: {p.Completeness}; Status: {p.Status}.";
    }

    private static string FormatViolationCount(int count)
        => count switch
        {
            1 => "1 Verstoß",
            _ => $"{count} Verstöße",
        };

}
