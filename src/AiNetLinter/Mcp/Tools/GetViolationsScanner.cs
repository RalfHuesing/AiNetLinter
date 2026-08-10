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

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Reine Formatierungs-/Filter-Logik fuer <see cref="GetViolationsTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetViolationsTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c>AiNetLinter.mdc</c>) klein bleibt.
/// Delegiert die eigentliche Lint-Arbeit an <see cref="LinterEngine.RunAsync(Solution, bool, int, CancellationToken)"/>
/// mit <c>noCache: true</c> — bewusst KEIN Neubau einer eigenen Lint-Loop, weil
/// <c>konzept.md</c> fuer <c>get_violations</c> explizit die <see cref="LinterEngine"/> als Basis
/// vorsieht und der Disk-Cache fuer den resident laufenden Server irrelevant ist (Muss-Haven "Cache
/// umgehen": Cache dient der Vermeidung von Re-Compilation zwischen unabhaengigen CLI-Prozessen).
/// Post-Filter auf den fertigen <see cref="RuleViolation"/>s (case-insensitive <c>Contains</c> auf
/// Projekt-Name oder solution-relativem Pfad), kein Pre-Filter ueber <see cref="LinterArgs"/> —
/// reine Funktionen ohne <see cref="McpCodeGraphServer"/>-Abhaengigkeit, direkt unit-testbar.
/// </summary>
internal static class GetViolationsScanner
{
    /// <summary>
    /// Baut den vollstaendigen Lint-Violations-Report fuer <paramref name="solution"/>. Ist
    /// <paramref name="scopeFilter"/> gesetzt, aber matched keine Datei, wird eine explizite
    /// "Keine Dateien im Scope"-Meldung geliefert statt der sonst irrefuehrenden "keine Violations"-
    /// Aussage. Defensive <c>try/catch</c> defensiv fuer unerwartete Lint-Errors —
    /// ueberspringt die betroffene Datei nicht moeglich (der Fehler waere global). Der
    /// <see cref="GetViolationsResult.IsMalfunction"/>-Flag signalisiert
    /// <see cref="GetViolationsTool"/>, dass dieser Fall (anders als "Keine Dateien im Scope" oder
    /// "0 Violations") eine echte Malfunction ist und laut IsErrorPolicy.md mit IsError=true
    /// beantwortet werden muss.
    /// </summary>
    internal static async Task<GetViolationsResult> BuildViolationsTextAsync(GetViolationsScannerParameters p)
    {
        var solution = p.Solution;
        var config = p.Config;
        var console = p.Console;
        var scopeFilter = p.ScopeFilter;
        var ct = p.CancellationToken;
        var usedDefaultConfig = p.UsedDefaultConfig;
        // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik fuer `with {...}`
        // und durchgereichte Sub-Properties); ILinterEngineConfig wird projektweit ausschliesslich
        // von Config implementiert, der Downcast ist daher nicht spekulativ.
        var concreteConfig = (Config)config;

        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var fileToProject = ViolationScopeFilter.BuildFileToProjectMap(solution, solutionDir);

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
            // Message + separates Context-Feld statt String-Verkettung — konsistent mit dem
            // etablierten Muster in FindSymbolTool/FindReferencesTool/GetSymbolBodyTool/
            // GetImpactTool (siehe deren catch-Bloecke bzw. GitDiffFailedException-Handling).
            return new GetViolationsResult(
                "Unerwarteter Fehler bei der Lint-Analyse.",
                IsMalfunction: true,
                Context: ex.Message);
        }

        return new GetViolationsResult(
            FormatReport(solutionDir, fileToProject, violations, scopeFilter, usedDefaultConfig),
            IsMalfunction: false,
            // Gleiche Filter-/Sortierlogik wie FormatReport (ueber ViolationScopeFilter geteilt) —
            // StructuredContent zeigt exakt die Violations, die auch im Text-Report auftauchen.
            Violations: ViolationScopeFilter.FilterAndSortViolations(solutionDir, fileToProject, violations, scopeFilter));
    }

    // ainetlinter-disable MaxMethodParameterCount — FormatReport kapselt einen Report-Bau
    // aus 5 unabhaengigen Eingaben (Verzeichnis, File-Map, Violations, Filter, Config-Source).
    // Ein Parameter-Record wuerde die AIContextFootprint-Abhaengigkeiten von
    // AnalysisToolRegistrations ueber das projektweite 2800-Limit treiben, weil die
    // Aufrufstelle in BuildViolationsTextAsync bereits eine zentrale Parameter-Bundelung
    // (GetViolationsScannerParameters) besitzt — die hier gebuendelt wuerde, ohne semantischen
    // Mehrwert. Direkter Test-Zugriff erfordert internal-Sichtbarkeit, die wiederum die
    // private-Relaxation (MaxMethodParameterCountForNonPublic) verliert.
    internal static string FormatReport(
        string solutionDir,
        Dictionary<string, string> fileToProject,
        IReadOnlyCollection<RuleViolation> violations,
        string? scopeFilter,
        bool usedDefaultConfig)
    {
        var filtered = ViolationScopeFilter.FilterAndSortViolations(solutionDir, fileToProject, violations, scopeFilter);
        var matchingFileCount = ViolationScopeFilter.CountMatchingFiles(fileToProject, solutionDir, scopeFilter);

        if (matchingFileCount == 0 && !string.IsNullOrWhiteSpace(scopeFilter))
        {
            return $"Keine Dateien im Scope (Filter: '{scopeFilter}') — Filter pruefen.";
        }

        var sb = new StringBuilder();
        if (usedDefaultConfig)
        {
            // Sichtbarer Marker fuer den Agent-LLM: die Lint-Ergebnisse stammen NICHT aus der
            // projekteigenen rules.json (sondern aus den Code-Defaults). Wird nur dann
            // ausgegeben, wenn der Server ohne --config gestartet wurde und neben der Solution
            // keine rules.json gefunden hat.
            sb.AppendLine("Basis: Default-Regeln, keine rules.json gefunden");
            sb.AppendLine();
        }
        var scopeSuffix = string.IsNullOrWhiteSpace(scopeFilter) ? "" : $" | Scope-Filter: '{scopeFilter}'";
        sb.AppendLine($"Lint-Violations: {filtered.Count} Verstoesse in {matchingFileCount} Dateien im Scope{scopeSuffix}");
        sb.AppendLine();

        if (filtered.Count == 0)
        {
            sb.AppendLine("Keine Lint-Violations.");
            return sb.ToString().TrimEnd();
        }

        var errors = filtered.Where(v => ResolveSeverity(v) == "error").ToList();
        var warnings = filtered.Where(v => ResolveSeverity(v) != "error").ToList();

        AppendSection(sb, "Fehler", errors, solutionDir);
        AppendSection(sb, "Warnungen", warnings, solutionDir);

        return sb.ToString().TrimEnd();
    }

    private static string ResolveSeverity(RuleViolation v)
    {
        if (!string.IsNullOrEmpty(v.EffectiveSeverity)) return v.EffectiveSeverity;
        return RuleRegistry.TryResolve(v.RuleName)?.Severity ?? "warning";
    }

    private static void AppendSection(
        StringBuilder sb, string heading, IReadOnlyList<RuleViolation> violations, string solutionDir)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine();

        if (violations.Count == 0)
        {
            sb.AppendLine("Keine.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Datei | Zeile | Regel | Details |");
        sb.AppendLine("|:---|---:|:---|:---|");
        foreach (var v in violations.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.LineNumber)
                                    .ThenBy(x => x.RuleName, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
            sb.AppendLine($"| {relativePath} | {v.LineNumber} | {v.RuleName} | {v.Details} |");
        }
        sb.AppendLine();
    }

}

/// <summary>
/// Parameter-Record fuer <see cref="GetViolationsScanner.BuildViolationsTextAsync"/>. Kapselt
/// 6 Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// (siehe <c>AiNetLinter.mdc</c>) eingehalten wird.
/// </summary>
internal sealed record GetViolationsScannerParameters(
    Solution Solution,
    ILinterEngineConfig Config,
    ILintConsole Console,
    string? ScopeFilter,
    CancellationToken CancellationToken,
    bool UsedDefaultConfig = false);

/// <summary>
/// Ergebnis-Record fuer <see cref="GetViolationsScanner.BuildViolationsTextAsync"/>.
/// <paramref name="IsMalfunction"/> unterscheidet eine echte Malfunction (unerwartete Exception
/// in der LinterEngine — <see cref="GetViolationsTool"/> antwortet dafuer mit IsError=true, siehe
/// IsErrorPolicy.md) von einem normalen Report-Text (auch "Keine Dateien im Scope" oder "0
/// Violations" zaehlen als normal, nicht als Malfunction). <paramref name="Context"/> traegt bei
/// einer Malfunction die rohe Exception-Message (analog zum <c>context:</c>-Parameter von
/// <see cref="McpToolResults.Error"/>/<see cref="McpToolResults.Recoverable"/> in den anderen
/// Tools) — bleibt <see langword="null"/> fuer normale Reports. <paramref name="Violations"/>
/// traegt die gefilterten/sortierten Violations fuer <c>StructuredContent</c> (S1.3) — bleibt
/// <see langword="null"/> bei einer Malfunction (kein sinnvoller Teil-Payload).
/// </summary>
internal sealed record GetViolationsResult(
    string Text, bool IsMalfunction, string? Context = null, IReadOnlyList<RuleViolation>? Violations = null);
