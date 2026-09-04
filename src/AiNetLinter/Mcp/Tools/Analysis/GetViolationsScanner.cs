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

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// Reine Formatierungs-/Filter-Logik fuer <see cref="GetViolationsTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetViolationsTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c>AiNetLinter.mdc</c>) klein bleibt.
/// Delegiert die eigentliche Lint-Arbeit an <see cref="LinterEngine.RunAsync(Solution, bool, int, CancellationToken)"/>
/// mit <c>noCache: true</c> — bewusst KEIN Neubau einer eigenen Lint-Loop, weil
/// vorsieht und der Disk-Cache fuer den resident laufenden Server irrelevant ist
/// (Cache dient der Vermeidung von Re-Compilation zwischen unabhaengigen CLI-Prozessen).
/// Post-Filter auf den fertigen <see cref="RuleViolation"/>s (case-insensitive <c>Contains</c> auf
/// Projekt-Name oder solution-relativem Pfad), kein Pre-Filter ueber <see cref="LinterArgs"/> —
/// reine Funktionen ohne <see cref="McpCodeGraphServer"/>-Abhaengigkeit, direkt unit-testbar.
/// </summary>
internal static class GetViolationsScanner
{
    /// <summary>
    /// Default-Obergrenze fuer die Anzahl gezeigter Violations (Text-Tabellen und
    /// StructuredContent gleichermassen) — analog zu <c>find_symbol</c>/<c>find_references</c>
    /// und <see cref="PatternDetect.PatternDetectScanner.DefaultMaxResultsPerPattern"/>. Vor
    /// Einfuehrung dieses Limits gab <c>get_violations</c> auf einer Solution mit vielen
    /// bestehenden Verstoessen (z. B. beim Erstlauf gegen ein fremdes Projekt) die komplette,
    /// unbegrenzte Liste zurueck — konnte den Client-Token-Guard sprengen und den gesamten
    /// Tool-Call zum Scheitern bringen (dieselbe Bug-Klasse wie bei <c>get_hotspots</c> vor dessen
    /// Fix, siehe Commit-Historie).
    /// </summary>
    internal const int DefaultMaxResults = 50;

    /// <summary>
    /// Baut den Lint-Violations-Report fuer <paramref name="solution"/>, trunkiert auf
    /// <paramref name="p.MaxResults"/>. Ist <paramref name="scopeFilter"/> gesetzt, aber matched
    /// keine Datei, wird eine explizite "Keine Dateien im Scope"-Meldung geliefert statt der sonst
    /// irrefuehrenden "keine Violations"-Aussage. Defensive <c>try/catch</c> defensiv fuer
    /// unerwartete Lint-Errors — ueberspringt die betroffene Datei nicht moeglich (der Fehler waere
    /// global). Der <see cref="GetViolationsResult.IsMalfunction"/>-Flag signalisiert
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
        var maxResults = p.MaxResults < 1 ? 1 : p.MaxResults;
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var fileToProject = ViolationScopeFilter.BuildFileToProjectMap(solution, solutionDir);

        IReadOnlyCollection<RuleViolation> violations;
        try
        {
            violations = await RunSolutionLintAsync(solution, config, console, ct);
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

        var filterOptions = new ViolationFilterOptions(scopeFilter, p.RuleId, p.MinSeverity);
        var filtered = ViolationScopeFilter.FilterAndSortViolations(solutionDir, fileToProject, violations, filterOptions);
        var isTruncated = filtered.Count > maxResults;

        IReadOnlyList<RuleViolation> finalViolations;
        if (p.IncludeSnippet)
        {
            var contextLines = Math.Clamp(p.ContextLines, 0, 5);
            var enriched = new List<RuleViolation>(filtered.Count);
            foreach (var v in filtered)
            {
                ct.ThrowIfCancellationRequested();
                var snippet = await ExtractSnippetAsync(solution, v.FilePath, v.LineNumber, contextLines, ct);
                enriched.Add(v with { Snippet = snippet });
            }
            finalViolations = enriched;
        }
        else
        {
            finalViolations = filtered;
        }

        var reportText = FormatReport(solutionDir, fileToProject, finalViolations, filterOptions, usedDefaultConfig, maxResults);
        var shown = isTruncated ? finalViolations.Take(maxResults).ToList() : finalViolations;

        return new GetViolationsResult(
            reportText,
            IsMalfunction: false,
            IsTruncated: isTruncated,
            // Gleiche Trunkierung wie FormatReport — StructuredContent zeigt exakt die Violations,
            // die auch im Text-Report auftauchen.
            Violations: shown);
    }

    /// <summary>
    /// Einzige Stelle, die die <see cref="LinterEngine"/> konstruiert und solutionweit laufen laesst —
    /// geteilt von <c>get_violations</c> und der diff-bezogenen Violations-Stufe
    /// (<see cref="DiffViolationScanner"/>), damit es genau eine Engine-Beschaffung gibt. Immer mit
    /// <c>noCache: true</c>: der Disk-Cache ist fuer den resident laufenden Server irrelevant (er dient
    /// der Vermeidung von Re-Compilation zwischen unabhaengigen CLI-Prozessen).
    /// </summary>
    internal static async Task<IReadOnlyCollection<RuleViolation>> RunSolutionLintAsync(
        Solution solution, ILinterEngineConfig config, ILintConsole console, CancellationToken ct)
    {
        // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik fuer `with {...}`
        // und durchgereichte Sub-Properties); ILinterEngineConfig wird projektweit ausschliesslich
        // von Config implementiert, der Downcast ist daher nicht spekulativ.
        var concreteConfig = (Config)config;
        var engine = new LinterEngine(
            config: concreteConfig,
            rulesJsonContent: null,
            profiler: null,
            console: console);
        return await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);
    }

    private static async Task<string?> ExtractSnippetAsync(
        Solution solution, string filePath, int lineNumber, int contextLines, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(filePath) || lineNumber < 1) return null;

        var doc = solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        Microsoft.CodeAnalysis.Text.SourceText? sourceText = null;
        if (doc is not null)
        {
            sourceText = await doc.GetTextAsync(ct);
        }
        else if (File.Exists(filePath))
        {
            var fileContent = await File.ReadAllTextAsync(filePath, ct);
            sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(fileContent);
        }

        if (sourceText is null) return null;

        var startLineIndex = Math.Max(0, lineNumber - 1 - contextLines);
        var endLineIndex = Math.Min(sourceText.Lines.Count - 1, lineNumber - 1 + contextLines);
        if (endLineIndex - startLineIndex + 1 > 15)
        {
            endLineIndex = startLineIndex + 14;
        }

        var sb = new StringBuilder();
        for (int i = startLineIndex; i <= endLineIndex; i++)
        {
            var lineStr = sourceText.Lines[i].ToString();
            var lineNum = i + 1;
            sb.AppendLine($"{lineNum,4} | {lineStr}");
        }
        return sb.ToString().TrimEnd();
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
        bool usedDefaultConfig,
        int maxResults = DefaultMaxResults) =>
        FormatReport(solutionDir, fileToProject, violations, new ViolationFilterOptions(scopeFilter), usedDefaultConfig, maxResults);

    internal static string FormatReport(
        string solutionDir,
        Dictionary<string, string> fileToProject,
        IReadOnlyCollection<RuleViolation> violations,
        ViolationFilterOptions filterOptions,
        bool usedDefaultConfig,
        int maxResults = DefaultMaxResults)
    {
        var filtered = ViolationScopeFilter.FilterAndSortViolations(solutionDir, fileToProject, violations, filterOptions);
        var matchingFileCount = ViolationScopeFilter.CountMatchingFiles(fileToProject, solutionDir, filterOptions.ScopeFilter);

        if (matchingFileCount == 0 && !string.IsNullOrWhiteSpace(filterOptions.ScopeFilter))
        {
            return $"Keine Dateien im Scope (Filter: '{filterOptions.ScopeFilter}') — Filter pruefen.";
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
        var filterDetails = new List<string>();
        if (!string.IsNullOrWhiteSpace(filterOptions.ScopeFilter)) filterDetails.Add($"Scope: '{filterOptions.ScopeFilter}'");
        if (!string.IsNullOrWhiteSpace(filterOptions.RuleId)) filterDetails.Add($"Regel: '{filterOptions.RuleId}'");
        if (!string.IsNullOrWhiteSpace(filterOptions.MinSeverity)) filterDetails.Add($"Min-Severity: '{filterOptions.MinSeverity}'");
        var scopeSuffix = filterDetails.Count > 0 ? " | " + string.Join(", ", filterDetails) : "";
        sb.AppendLine($"Lint-Violations: {filtered.Count} Verstoesse in {matchingFileCount} Dateien im Scope{scopeSuffix}");
        sb.AppendLine();

        if (filtered.Count == 0)
        {
            sb.AppendLine("Keine Lint-Violations.");
            return sb.ToString().TrimEnd();
        }

        // Trunkierung VOR der Fehler-/Warnungs-Aufteilung, damit beide Sektionen zusammen nie
        // mehr als maxResults Zeilen zeigen — ohne dieses Limit gab eine Solution mit vielen
        // bestehenden Verstoessen (typisch beim Erstlauf gegen ein fremdes Projekt) die komplette
        // Liste zurueck und konnte den Client-Token-Guard sprengen (siehe DefaultMaxResults-Doc).
        var isTruncated = filtered.Count > maxResults;
        var shown = isTruncated ? filtered.Take(maxResults).ToList() : filtered;

        var errors = shown.Where(v => RuleRegistry.ResolveSeverity(v) == "error").ToList();
        var warnings = shown.Where(v => RuleRegistry.ResolveSeverity(v) != "error").ToList();

        AppendSection(sb, "Fehler", errors, solutionDir);
        AppendSection(sb, "Warnungen", warnings, solutionDir);

        if (isTruncated)
        {
            sb.AppendLine($"[{filtered.Count} Verstoesse gesamt, {maxResults} gezeigt — scopeFilter verfeinern oder maxResults erhoehen]");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendSection(
        StringBuilder sb, string heading, IReadOnlyList<RuleViolation> violations, string solutionDir)
    {
        var mb = new MarkdownBuilder();
        mb.Heading(2, heading).BlankLine();

        if (violations.Count == 0)
        {
            mb.AppendTo(sb);
            sb.Append("Keine.\n\n");
            return;
        }

        var table = new MarkdownTableBuilder()
            .AddColumn("Datei")
            .AddColumn("Zeile", ColumnAlign.Right)
            .AddColumn("Regel")
            .AddColumn("Details");

        mb.Line(table.BuildHeaderLine());
        mb.Line(table.BuildSeparatorLine());

        foreach (var v in violations.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.LineNumber)
                                    .ThenBy(x => x.RuleName, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
            mb.Line(table.BuildRowLine(relativePath, v.LineNumber.ToString(), v.RuleName ?? string.Empty, v.Details ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(v.Snippet))
            {
                mb.CodeBlock("csharp", v.Snippet!);
                mb.BlankLine();
            }
        }

        mb.AppendTo(sb);
        sb.Append('\n');
    }

}

/// <summary>
/// Parameter-Record fuer <see cref="GetViolationsScanner.BuildViolationsTextAsync"/>. Kapselt
/// 9 Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// (siehe <c>AiNetLinter.mdc</c>) eingehalten wird.
/// </summary>
internal sealed record GetViolationsScannerParameters(
    Solution Solution,
    ILinterEngineConfig Config,
    ILintConsole Console,
    string? ScopeFilter,
    CancellationToken CancellationToken,
    bool UsedDefaultConfig = false,
    int MaxResults = GetViolationsScanner.DefaultMaxResults,
    int ContextLines = 0,
    bool IncludeSnippet = false,
    string? RuleId = null,
    string? MinSeverity = null);

/// <summary>
/// Ergebnis-Record fuer <see cref="GetViolationsScanner.BuildViolationsTextAsync"/>.
/// <paramref name="IsMalfunction"/> unterscheidet eine echte Malfunction (unerwartete Exception
/// in der LinterEngine — <see cref="GetViolationsTool"/> antwortet dafuer mit IsError=true, siehe
/// IsErrorPolicy.md) von einem normalen Report-Text (auch "Keine Dateien im Scope" oder "0
/// Violations" zaehlen als normal, nicht als Malfunction). <paramref name="Context"/> traegt bei
/// einer Malfunction die rohe Exception-Message (analog zum <c>context:</c>-Parameter von
/// <see cref="McpToolResults.Error"/>/<see cref="McpToolResults.Recoverable"/> in den anderen
/// Tools) — bleibt <see langword="null"/> fuer normale Reports. <paramref name="IsTruncated"/>
/// zeigt an, ob die Gesamtzahl der Violations das <c>maxResults</c>-Limit ueberschritten hat —
/// steuert in <see cref="GetViolationsTool"/> die Wahl zwischen Sufficiency-Hinweis und
/// Trunkierungs-Meta (analog zum <c>isTruncated</c>-Muster in <c>FindReferencesTool</c>).
/// <paramref name="Violations"/> traegt die gefilterten/sortierten/trunkierten Violations fuer
/// <c>StructuredContent</c> — bleibt <see langword="null"/> bei einer Malfunction (kein
/// sinnvoller Teil-Payload).
/// </summary>
internal sealed record GetViolationsResult(
    string Text,
    bool IsMalfunction,
    bool IsTruncated = false,
    string? Context = null,
    IReadOnlyList<RuleViolation>? Violations = null);
