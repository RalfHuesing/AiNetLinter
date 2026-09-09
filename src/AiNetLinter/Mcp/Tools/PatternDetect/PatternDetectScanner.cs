#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.PatternDetect;

/// <summary>
/// Baut den <c>pattern_detect</c>-Report: gruppiert die von der bereits laufenden
/// <see cref="LinterEngine"/> erzeugten <see cref="RuleViolation"/>-Objekte nach
/// <see cref="PatternCatalog"/>-Eintrag statt der flachen Datei-für-Datei-Liste von
/// <c>get_violations</c>. Scope-Filter-/Sortierlogik gemeinsam mit <c>GetViolationsScanner</c>
/// über <see cref="ViolationScopeFilter"/> — nur die Pattern-Gruppierung selbst ist
/// <c>pattern_detect</c>-spezifisch.
/// </summary>
internal static class PatternDetectScanner
{
    internal const int DefaultMaxResultsPerPattern = 20;

    internal static async Task<PatternDetectResult> BuildReportAsync(PatternDetectScannerParameters p)
    {
        var solution = p.Solution;
        var console = p.Console;
        var scopeFilter = p.ScopeFilter;
        var ct = p.CancellationToken;
        // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik) — ILinterEngineConfig
        // wird projektweit ausschliesslich von Config implementiert (siehe GetViolationsScanner).
        var concreteConfig = (Config)p.Config;

        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var fileToProject = ViolationScopeFilter.BuildFileToProjectMap(solution, solutionDir, concreteConfig.FileFilters);

        IReadOnlyCollection<RuleViolation> violations;
        try
        {
            var engine = new LinterEngine(
                config: concreteConfig,
                configContent: null,
                profiler: null,
                console: console);
            violations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PatternDetectResult(null, null, IsMalfunction: true, Context: ex.Message);
        }

        var scoped = ViolationScopeFilter.FilterAndSortViolations(solutionDir, fileToProject, violations, scopeFilter);
        var matchingFileCount = ViolationScopeFilter.CountMatchingFiles(fileToProject, solutionDir, scopeFilter);
        var configurationScope = BuildConfigurationScope(solution, solutionDir, scopeFilter, concreteConfig);

        var reports = p.Patterns
            .Select(pattern => BuildPatternReport(new PatternReportParameters(
                pattern, scoped, solutionDir, configurationScope, matchingFileCount, scopeFilter, p.MaxResultsPerPattern)))
            .ToList();

        var text = FormatReport(matchingFileCount, scopeFilter, reports, p.MaxResultsPerPattern);
        var payload = new PatternDetectPayload(
            reports.Select(r => r.Entry).ToList(),
            new PatternDetectSummary(
                PatternsWithHits: reports.Count(r => r.Entry.Occurrences > 0),
                TotalOccurrences: reports.Sum(r => r.Entry.Occurrences),
                Completeness: DetermineCompleteness(reports)));

        return new PatternDetectResult(text, payload, IsMalfunction: false);
    }

    private static PatternConfigurationScope BuildConfigurationScope(
        Solution solution, string solutionDir, string? scopeFilter, Config config)
    {
        var matchingDocuments = solution.Projects
            .Where(project => project.SupportsCompilation)
            .SelectMany(project => project.Documents)
            .Where(document => SourceFileCatalog.IsValidDocument(document, solutionDir)
                && ViolationScopeFilter.MatchesScope(
                    document.FilePath ?? document.Name, document.Project.Name, solutionDir, scopeFilter))
            .ToList();
        var analyzableDocuments = matchingDocuments
            .Where(document => !FileFilterEvaluator.IsExcluded(document.FilePath ?? document.Name, config.FileFilters))
            .ToList();
        var effectiveConfigs = analyzableDocuments
            .Select(document => ProjectConfigResolver.ResolveForDocument(document, config, solutionDir))
            .ToList();

        return new PatternConfigurationScope(
            effectiveConfigs);
    }

    private static PatternReportBuild BuildPatternReport(PatternReportParameters report)
    {
        var ordered = report.ScopedViolations
            .Where(v => report.Pattern.RuleIds.Contains(v.RuleName))
            .OrderBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var shown = ordered.Take(report.MaxResultsPerPattern).ToList();
        var configuration = DetermineConfiguration(report.Pattern, report.ConfigurationScope);
        var isTruncated = shown.Count < ordered.Count;
        var status = DetermineStatus(configuration.Status, ordered.Count, isTruncated);
        var entry = new PatternResultEntry(
            report.Pattern.Id,
            report.Pattern.Description,
            ordered.Count,
            shown.Select(v => ToItem(report.SolutionDir, v)).ToList(),
            status,
            DetermineCause(configuration, status, ordered.Count, shown.Count, report),
            DetermineConfidence(configuration.Status, isTruncated),
            BuildNextAction(status),
            isTruncated ? ordered.Count - shown.Count : 0);

        return new PatternReportBuild(entry, ordered.Select(v => FormatLine(report.SolutionDir, v)).ToList());
    }

    private static PatternConfigurationState DetermineConfiguration(
        PatternDefinition pattern, PatternConfigurationScope scope)
    {
        var metadata = pattern.RuleIds.Select(RuleRegistry.TryResolve).ToList();
        if (metadata.Any(rule => rule is null))
        {
            return new PatternConfigurationState(
                "not_decidable", "Mindestens eine zugeordnete Regel ist im Vertragskatalog nicht aufloesbar.");
        }

        if (scope.EffectiveConfigs.Count == 0)
        {
            return new PatternConfigurationState(
                "not_decidable",
                "Keine analysierbaren Dokumente im angeforderten Scope; die effektive Projekt-/Pfad-Konfiguration ist nicht entscheidbar.");
        }

        var resolvedRules = metadata.OfType<RuleMetadata>().ToList();
        var enabledStates = scope.EffectiveConfigs
            .SelectMany(config => resolvedRules.Select(rule => rule.IsEnabled(config)))
            .ToList();
        if (enabledStates.All(enabled => !enabled))
        {
            var ruleNames = string.Join(", ", resolvedRules.Select(rule => rule.ConfigKeyHint ?? rule.RuleId));
            return new PatternConfigurationState(
                "not_configured", $"Keine zugeordnete Regel ist in der effektiven Projekt-/Pfad-Konfiguration aktiviert ({ruleNames}).");
        }

        if (enabledStates.Any(enabled => !enabled))
        {
            return new PatternConfigurationState(
                "not_decidable",
                "Zugeordnete Regeln sind über Projekt-/Pfad-Konfigurationen teils aktiviert und teils deaktiviert.");
        }

        return new PatternConfigurationState("configured", "");
    }

    private static string DetermineStatus(string configurationStatus, int occurrences, bool isTruncated) =>
        configurationStatus != "configured"
            ? configurationStatus
            : occurrences == 0 ? "empty" : isTruncated ? "truncated" : "checked";

    private static string DetermineCause(
        PatternConfigurationState configuration, string status, int occurrences, int shown,
        PatternReportParameters report)
    {
        if (status != "configured") return configuration.Cause;
        if (occurrences == 0)
        {
            var scope = string.IsNullOrWhiteSpace(report.ScopeFilter) ? "" : $" ('{report.ScopeFilter}')";
            return $"Keine Treffer in den {report.MatchingFileCount} Dateien des angeforderten Scopes{scope}; dies ist keine globale Abwesenheitsbehauptung.";
        }

        return shown < occurrences
            ? $"{shown} von {occurrences} Treffern gezeigt; die Ausgabe ist begrenzt."
            : $"Alle {occurrences} Treffer im angeforderten Scope wurden geprueft.";
    }

    private static string DetermineConfidence(string configurationStatus, bool isTruncated) =>
        configurationStatus != "configured" ? "low" : isTruncated ? "medium" : "high";

    private static PatternNextAction BuildNextAction(string status) => status switch
    {
        "truncated" => new PatternNextAction("continue", "maxResultsPerPattern erhoehen oder scopeFilter verfeinern."),
        "not_configured" => new PatternNextAction("configure", "Zugeordnete Regel aktivieren oder Regelkonfiguration pruefen."),
        "not_decidable" => new PatternNextAction("inspect", "Effektive Projekt-/Pfad-Konfiguration und Scope pruefen."),
        "empty" => new PatternNextAction("refine_scope", "Bei Bedarf scopeFilter oder die Regelkonfiguration aendern."),
        _ => new PatternNextAction("inspect", "Treffer im Detail pruefen; keine automatische Aenderung ableiten."),
    };

    private sealed record PatternReportParameters(
        PatternDefinition Pattern,
        IReadOnlyList<RuleViolation> ScopedViolations,
        string SolutionDir,
        PatternConfigurationScope ConfigurationScope,
        int MatchingFileCount,
        string? ScopeFilter,
        int MaxResultsPerPattern);

    private sealed record PatternConfigurationScope(
        IReadOnlyList<Config> EffectiveConfigs);

    private sealed record PatternConfigurationState(string Status, string Cause);

    private static string DetermineCompleteness(IReadOnlyList<PatternReportBuild> reports)
    {
        if (reports.Any(r => r.Entry.Status == "not_decidable")) return "not_decidable";
        if (reports.Any(r => r.Entry.Status == "not_configured")) return "not_configured";
        if (reports.Any(r => r.Entry.Status == "truncated")) return "truncated";
        return reports.All(r => r.Entry.Status == "empty") ? "empty" : "complete";
    }

    private static PatternItemEntry ToItem(string solutionDir, RuleViolation v)
    {
        return new PatternItemEntry(
            Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/'), v.LineNumber, v.RuleName, v.Details);
    }

    private static string FormatLine(string solutionDir, RuleViolation v)
    {
        var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
        return $"{relativePath}:{v.LineNumber} - {v.RuleName}: {v.Details}";
    }

    private static string FormatReport(
        int matchingFileCount, string? scopeFilter, IReadOnlyList<PatternReportBuild> reports, int maxResultsPerPattern)
    {
        var scopeSuffix = string.IsNullOrWhiteSpace(scopeFilter) ? "" : $" | Scope-Filter: '{scopeFilter}'";
        var patternsWithHits = reports.Count(r => r.Entry.Occurrences > 0);
        var totalOccurrences = reports.Sum(r => r.Entry.Occurrences);

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Pattern-Detect: {patternsWithHits} von {reports.Count} Patterns mit Treffern, " +
            $"{totalOccurrences} Treffer gesamt in {matchingFileCount} Dateien im Scope{scopeSuffix}");
        var completeness = DetermineCompleteness(reports);
        sb.AppendLine($"Vollstaendigkeitsstatus: {completeness switch
        {
            "complete" => "vollstaendig",
            "empty" => "leer",
            "truncated" => "begrenzt",
            "not_configured" => "nicht konfiguriert",
            _ => "nicht entscheidbar"
        }}");
        sb.AppendLine();

        foreach (var report in reports)
        {
            sb.AppendLine($"## {report.Entry.Id} — {report.Entry.Description} [{report.Entry.Status}, confidence={report.Entry.Confidence}]");
            sb.AppendLine($"Ursache: {report.Entry.Cause}");
            sb.AppendLine($"Naechster Schritt: {report.Entry.Next.Action} — {report.Entry.Next.Reason}");
            sb.AppendLine();
            sb.AppendLine(report.Entry.Occurrences == 0
                ? "Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim."
                : McpTruncation.TruncateLines(report.Lines, report.Entry.Occurrences, maxResultsPerPattern));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Interner Baustein zwischen Report-Aufbau und Formatierung: <see cref="Entry"/> ist
    /// das bereits auf <c>maxResultsPerPattern</c> gekappte Struktur-Item, <see cref="Lines"/> die
    /// vollstaendige (ungekappte) Textzeilen-Liste fuer <see cref="McpTruncation.TruncateLines"/>.</summary>
    private sealed record PatternReportBuild(PatternResultEntry Entry, IReadOnlyList<string> Lines);
}

/// <summary>
/// Parameter-Record fuer <see cref="PatternDetectScanner.BuildReportAsync"/>. Kapselt 6
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c> (siehe
/// <c>AiNetLinter.mdc</c>) eingehalten wird (Pattern 1:1 von <c>GetViolationsScannerParameters</c>).
/// </summary>
internal sealed record PatternDetectScannerParameters(
    Solution Solution,
    ILinterEngineConfig Config,
    ILintConsole Console,
    string? ScopeFilter,
    IReadOnlyList<PatternDefinition> Patterns,
    CancellationToken CancellationToken,
    int MaxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern);

/// <summary>
/// Ergebnis-Record fuer <see cref="PatternDetectScanner.BuildReportAsync"/>. <see cref="IsMalfunction"/>
/// unterscheidet eine echte Malfunction (unerwartete LinterEngine-Exception, <see cref="Context"/>
/// non-null, <see cref="Payload"/> null) von einem normalen Report. Auch ein leerer oder
/// nicht entscheidbarer Scope bleibt ein normaler Report und traegt pro Pattern Statusmetadaten.
/// </summary>
internal sealed record PatternDetectResult(string? Text, PatternDetectPayload? Payload, bool IsMalfunction, string? Context = null);

/// <summary>Structured-Content-Wurzel fuer <c>pattern_detect</c> (Praezedenzfall, siehe
/// <c>SafeguardTool</c>): ein Eintrag je <see cref="PatternCatalog"/>-Pattern plus Gesamt-Summary.</summary>
internal sealed record PatternDetectPayload(IReadOnlyList<PatternResultEntry> Patterns, PatternDetectSummary Summary);

/// <summary>Ein Pattern-Treffer-Block: <see cref="Occurrences"/> ist die volle (ungekappte)
/// Trefferzahl, <see cref="Items"/> ist auf <c>maxResultsPerPattern</c> gekappt (analog zur
/// Text-Trunkierung via <see cref="McpTruncation"/>).</summary>
internal sealed record PatternResultEntry(
    string Id, string Description, int Occurrences, IReadOnlyList<PatternItemEntry> Items,
    string Status, string Cause, string Confidence, PatternNextAction Next, int TruncatedBy);

internal sealed record PatternNextAction(string Action, string Reason);

/// <summary>1:1-Mapping aus <see cref="RuleViolation"/> fuer den JSON-Schema-Output.</summary>
internal sealed record PatternItemEntry(string FilePath, int Line, string RuleName, string Details);

/// <summary>Gesamt-Summary ueber alle Patterns: <see cref="PatternsWithHits"/> zaehlt Patterns mit
/// mindestens einem Treffer, <see cref="TotalOccurrences"/> die volle (ungekappte) Trefferzahl
/// ueber alle Patterns summiert.</summary>
internal sealed record PatternDetectSummary(int PatternsWithHits, int TotalOccurrences, string Completeness);
