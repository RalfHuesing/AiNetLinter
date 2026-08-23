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
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// Interne Violations-Stufe des diff-bezogenen Analysepfads: ein Aufruf fuehrt den Linter GENAU EINMAL
/// solutionweit aus (<see cref="GetViolationsScanner.RunSolutionLintAsync"/>) und filtert das Ergebnis
/// rein diffbezogen — eine Violation bleibt, wenn ihre Datei+Zeile in einem geaenderten Hunk ODER in der
/// Deklarationsspanne eines gezeigten geaenderten Symbols liegt; alle anderen Violations derselben
/// Datei bleiben außen vor. Bei leerem Input (keine Hunks UND keine gezeigten Symbole) entfaellt der
/// Lauf samt Zaehler-Inkrement — dieselbe Skip-empty-Semantik wie der gebatchte Testzuordnungs-Scan.
/// </summary>
internal static class DiffViolationScanner
{
    /// <summary>
    /// Stufe: genau ein Lint-Lauf pro Aufruf, danach reine In-Memory-Filterung (kein scopeFilter, keine
    /// Trunkierung). Der optionale Zaehler wird unmittelbar vor dem Lauf inkrementiert — er misst
    /// ausgefuehrte Stufen, auch wenn der Lauf fehlschlaegt. Eine unerwartete non-OCE-Exception des
    /// Laufs liefert <see cref="DiffViolationScanResult.IsMalfunction"/> mit roher Exception-Message
    /// statt einer Teil-Violation-Liste (Muster <see cref="GetViolationsResult"/>).
    /// </summary>
    internal static async Task<DiffViolationScanResult> CollectAsync(DiffViolationScanRequest request)
    {
        // Kein Ziel -> weder Lint-Lauf noch Inkrement: der Zaehler misst ausgefuehrte Stufen,
        // nicht Aufrufe (dieselbe Leerpruefung wie im gebatchten Testzuordnungs-Scan).
        if (request.ChangedFiles.Count == 0 && request.ShownSymbols.Count == 0)
        {
            return new DiffViolationScanResult([]);
        }

        if (request.Counters is { } counters)
        {
            Interlocked.Increment(ref counters.LintRuns);
        }

        IReadOnlyCollection<RuleViolation> violations;
        try
        {
            violations = await GetViolationsScanner.RunSolutionLintAsync(
                request.Solution, request.Config, request.Console, request.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DiffViolationScanResult([], IsMalfunction: true, Context: ex.Message);
        }

        var paths = new DiffPathContext(
            request.RepositoryRoot,
            Path.GetDirectoryName(request.Solution.FilePath) ?? "");
        return new DiffViolationScanResult(
            FilterDiffRelevantViolations(violations, request.ChangedFiles, request.ShownSymbols, paths));
    }

    /// <summary>
    /// Pure Filterregel ohne I/O: behaelt eine Violation genau dann, wenn ihre Datei+Zeile in einem
    /// Hunk einer geaenderten Datei liegt ODER innerhalb der Deklarationsspanne eines gezeigten Symbols
    /// derselben Datei; Doppelbedingung liefert genau einen Eintrag. Zeilen sind 1-basisch inklusive;
    /// ein <see cref="HunkRange"/> mit <c>LineCount = 0</c> expandiert zu keiner Zeile und matcht nie.
    /// Pfade werden zentral hier vergleichbar gemacht: Hunk-Eingaben sind repo-root-relativ (native
    /// Trenner), Symbol-Eingaben solution-relativ, Violation-Pfade absolut — verglichen wird auf
    /// normalisierten Absolutpfaden ordinal case-insensitive (Windows-Dateisystem). Ausgabe sortiert
    /// FilePath → Zeile → Regel analog zur Scope-Sortierung.
    /// </summary>
    internal static IReadOnlyList<RuleViolation> FilterDiffRelevantViolations(
        IReadOnlyCollection<RuleViolation> violations,
        IReadOnlyList<ChangedFileRange> changedFiles,
        IReadOnlyList<ChangedSymbolEntry> shownSymbols,
        DiffPathContext paths)
    {
        var hunksByFile = BuildHunksByFile(changedFiles, paths.RepositoryRoot);
        var spansByFile = BuildSpansByFile(shownSymbols, paths.SolutionDir);

        return violations
            .Where(v => IsDiffRelevant(v, hunksByFile, spansByFile))
            .OrderBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, List<HunkRange>> BuildHunksByFile(
        IReadOnlyList<ChangedFileRange> changedFiles, string repositoryRoot)
    {
        var map = new Dictionary<string, List<HunkRange>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changedFiles)
        {
            var key = ToComparablePath(repositoryRoot, file.FilePath);
            if (!map.TryGetValue(key, out var ranges))
            {
                ranges = [];
                map[key] = ranges;
            }
            ranges.AddRange(file.Ranges);
        }
        return map;
    }

    private static Dictionary<string, List<ChangedSymbolEntry>> BuildSpansByFile(
        IReadOnlyList<ChangedSymbolEntry> shownSymbols, string solutionDir)
    {
        var map = new Dictionary<string, List<ChangedSymbolEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in shownSymbols)
        {
            var key = ToComparablePath(solutionDir, symbol.FilePath);
            if (!map.TryGetValue(key, out var entries))
            {
                entries = [];
                map[key] = entries;
            }
            entries.Add(symbol);
        }
        return map;
    }

    private static bool IsDiffRelevant(
        RuleViolation violation,
        Dictionary<string, List<HunkRange>> hunksByFile,
        Dictionary<string, List<ChangedSymbolEntry>> spansByFile)
    {
        var filePath = Path.GetFullPath(violation.FilePath);
        if (hunksByFile.TryGetValue(filePath, out var ranges) && ranges.Any(r => HunkContains(r, violation.LineNumber)))
        {
            return true;
        }

        return spansByFile.TryGetValue(filePath, out var symbols)
            && symbols.Any(s => s.StartLine <= violation.LineNumber && violation.LineNumber <= s.EndLine);
    }

    // Halboffen [StartLine, StartLine+LineCount): LineCount = 0 expandiert zu keiner Zeile und matcht nie.
    private static bool HunkContains(HunkRange range, int lineNumber) =>
        range.StartLine <= lineNumber && lineNumber < range.StartLine + range.LineCount;

    private static string ToComparablePath(string baseDirectory, string relativeOrAbsolutePath) =>
        Path.IsPathRooted(relativeOrAbsolutePath) || baseDirectory.Length == 0
            ? Path.GetFullPath(relativeOrAbsolutePath)
            : Path.GetFullPath(Path.Combine(baseDirectory, relativeOrAbsolutePath));
}

/// <summary>
/// Parameter-Object der internen Violations-Stufe (<see cref="DiffViolationScanner.CollectAsync"/>).
/// <see cref="ShownSymbols"/> traegt bewusst die GEZEIGTEN (gekapppten) geaenderten Symbole — die
/// Spannen-Filterung folgt dem Antwortvertrag, nicht dem rohen Diff. Pfadbedeutungen:
/// <see cref="RepositoryRoot"/> ist Basis der repo-root-relativen Hunks, das Solution-Verzeichnis
/// (<see cref="Solution.FilePath"/>) Basis der solution-relativen Symbol-Pfade; Violations tragen
/// absolute Dokumentpfade.
/// </summary>
internal sealed record DiffViolationScanRequest(
    Solution Solution,
    ILinterEngineConfig Config,
    ILintConsole Console,
    string RepositoryRoot,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ShownSymbols,
    DiffImpactCounters? Counters = null,
    CancellationToken CancellationToken = default);

/// <summary>
/// Ergebnis-Record der internen Violations-Stufe. <paramref name="IsMalfunction"/> unterscheidet eine
/// echte Malfunction (unerwartete Exception im Lint-Lauf) von einem leeren/normalem Ergebnis;
/// <paramref name="Context"/> traegt bei einer Malfunction die rohe Exception-Message.
/// </summary>
internal sealed record DiffViolationScanResult(
    IReadOnlyList<RuleViolation> Violations,
    bool IsMalfunction = false,
    string? Context = null);

/// <summary>Die beiden Basisverzeichnisse der Pfadnormalisierung: Repo-Wurzel (Hunks) und Solution-Verzeichnis (Symbole).</summary>
internal readonly record struct DiffPathContext(string RepositoryRoot, string SolutionDir);
