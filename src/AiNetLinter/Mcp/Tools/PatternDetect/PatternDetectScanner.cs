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
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.PatternDetect;

/// <summary>
/// Baut den <c>pattern_detect</c>-Report: gruppiert die von der bereits laufenden
/// <see cref="LinterEngine"/> erzeugten <see cref="RuleViolation"/>-Objekte nach
/// <see cref="PatternCatalog"/>-Eintrag statt der flachen Datei-für-Datei-Liste von
/// <c>get_violations</c>. Referenzmuster 1:1 von <c>GetViolationsScanner</c> übernommen
/// (<see cref="LinterEngine.RunAsync"/> mit <c>noCache: true</c>, gleiche Scope-Filter-Semantik,
/// gleiche Sortierung Datei→Zeile→Regel) — bewusst dupliziert statt <c>GetViolationsScanner</c>
/// zu ändern/erweitern (eigene, unveränderte Zuständigkeit dort).
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
        var fileToProject = BuildFileToProjectMap(solution, solutionDir);

        IReadOnlyCollection<RuleViolation> violations;
        try
        {
            var engine = new LinterEngine(
                config: concreteConfig,
                rulesJsonContent: null,
                profiler: null,
                console: console,
                args: null);
            violations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PatternDetectResult(null, null, IsMalfunction: true, Context: ex.Message);
        }

        var scoped = violations
            .Where(v => LookupProjectName(fileToProject, v.FilePath) is { } projectName
                        && MatchesScope(v.FilePath, projectName, solutionDir, scopeFilter))
            .ToList();

        var matchingFileCount = fileToProject
            .Count(kvp => MatchesScope(kvp.Key, kvp.Value, solutionDir, scopeFilter));

        if (matchingFileCount == 0 && !string.IsNullOrWhiteSpace(scopeFilter))
        {
            return new PatternDetectResult(
                $"Keine Dateien im Scope (Filter: '{scopeFilter}') — Filter pruefen.", null, IsMalfunction: false);
        }

        var reports = p.Patterns
            .Select(pattern => BuildPatternReport(pattern, scoped, solutionDir, p.MaxResultsPerPattern))
            .ToList();

        var text = FormatReport(matchingFileCount, scopeFilter, reports, p.MaxResultsPerPattern);
        var payload = new PatternDetectPayload(
            reports.Select(r => r.Entry).ToList(),
            new PatternDetectSummary(
                PatternsWithHits: reports.Count(r => r.Entry.Occurrences > 0),
                TotalOccurrences: reports.Sum(r => r.Entry.Occurrences)));

        return new PatternDetectResult(text, payload, IsMalfunction: false);
    }

    private static PatternReportBuild BuildPatternReport(
        PatternDefinition pattern, IReadOnlyList<RuleViolation> scoped, string solutionDir, int maxResultsPerPattern)
    {
        var ordered = scoped
            .Where(v => pattern.RuleIds.Contains(v.RuleName))
            .OrderBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var shown = ordered.Take(maxResultsPerPattern).ToList();
        var entry = new PatternResultEntry(
            pattern.Id,
            pattern.Description,
            ordered.Count,
            shown.Select(v => ToItem(solutionDir, v)).ToList());

        return new PatternReportBuild(entry, ordered.Select(v => FormatLine(solutionDir, v)).ToList());
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
        sb.AppendLine();

        foreach (var report in reports)
        {
            sb.AppendLine($"## {report.Entry.Id} — {report.Entry.Description} ({report.Entry.Occurrences} Treffer)");
            sb.AppendLine();
            sb.AppendLine(report.Entry.Occurrences == 0
                ? "Keine."
                : McpTruncation.TruncateLines(report.Lines, report.Entry.Occurrences, maxResultsPerPattern));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static Dictionary<string, string> BuildFileToProjectMap(Solution solution, string solutionDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (document.FilePath is null) continue;
                map[document.FilePath] = project.Name;
            }
        }
        return map;
    }

    private static bool MatchesScope(string filePath, string projectName, string solutionDir, string? scopeFilter)
    {
        if (string.IsNullOrEmpty(scopeFilter)) return true;
        if (projectName.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase)) return true;

        var relativePath = Path.GetRelativePath(solutionDir, filePath);
        return relativePath.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static string? LookupProjectName(Dictionary<string, string> fileToProject, string filePath)
    {
        return fileToProject.TryGetValue(filePath, out var name) ? name : null;
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
/// non-null, <see cref="Payload"/> null) von einem normalen Report (auch "Keine Dateien im Scope"
/// oder 0 Treffer zaehlen als normal — dort ist <see cref="Payload"/> ebenfalls null, weil dann kein
/// strukturierter Report gebaut wird, aber <see cref="Text"/> die Erklaerung traegt).
/// </summary>
internal sealed record PatternDetectResult(string? Text, PatternDetectPayload? Payload, bool IsMalfunction, string? Context = null);

/// <summary>Structured-Content-Wurzel fuer <c>pattern_detect</c> (S1.3-Praezedenzfall, siehe
/// <c>SafeguardTool</c>): ein Eintrag je <see cref="PatternCatalog"/>-Pattern plus Gesamt-Summary.</summary>
internal sealed record PatternDetectPayload(IReadOnlyList<PatternResultEntry> Patterns, PatternDetectSummary Summary);

/// <summary>Ein Pattern-Treffer-Block: <see cref="Occurrences"/> ist die volle (ungekappte)
/// Trefferzahl, <see cref="Items"/> ist auf <c>maxResultsPerPattern</c> gekappt (analog zur
/// Text-Trunkierung via <see cref="McpTruncation"/>).</summary>
internal sealed record PatternResultEntry(string Id, string Description, int Occurrences, IReadOnlyList<PatternItemEntry> Items);

/// <summary>1:1-Mapping aus <see cref="RuleViolation"/> fuer den JSON-Schema-Output.</summary>
internal sealed record PatternItemEntry(string FilePath, int Line, string RuleName, string Details);

/// <summary>Gesamt-Summary ueber alle Patterns: <see cref="PatternsWithHits"/> zaehlt Patterns mit
/// mindestens einem Treffer, <see cref="TotalOccurrences"/> die volle (ungekappte) Trefferzahl
/// ueber alle Patterns summiert.</summary>
internal sealed record PatternDetectSummary(int PatternsWithHits, int TotalOccurrences);
