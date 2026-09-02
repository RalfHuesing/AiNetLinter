#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Core;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Reine Scan-/Formatierungslogik fuer <see cref="GetHotspotsTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetHotspotsTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c> klein bleibt.
/// Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Liefert dieselbe
/// Kennzahl wie <see cref="AiNetLinter.Maps.HotspotMapBuilder"/> (CLI-Map-Typ <c>--map hotspots</c>),
/// aber gegen die resident gehaltene <see cref="Solution"/> statt eines Einmal-Filesystem-Scans, damit
/// z. B. Test-Fixtures im selben Verzeichnisbaum nicht faelschlich mitgezaehlt werden
/// JIT-Kontext). Die zwei Schwellwert-Konstanten sind bewusst aus <see cref="AiNetLinter.Maps.HotspotMapBuilder"/>
/// dupliziert (beide Klassen bleiben so unabhaengig voneinander instanziierbar); die Tabellen-Formatierung
/// lebt jeweils privat in <see cref="AiNetLinter.Maps.HotspotMapBuilder"/> und hier (Schicht-Trennung:
/// <c>Maps →</c> darf nicht <c>Mcp.Tools</c> referenzieren, also kein gemeinsamer Helper).
/// </summary>
internal static class GetHotspotsScanner
{
    internal const int DefaultMaxResults = 50;
    internal const int MaxResultsCap = 200;
    internal const double DefaultMinLinePercentage = 80.0;
    internal const double MinLinePercentage = 0.0;
    internal const double MaxLinePercentage = 100.0;
    internal const string DefaultScopeType = "production";

    private const double CriticalThreshold = 0.95;

    internal static int NormalizeMaxResults(int maxResults) =>
        Math.Clamp(maxResults < 1 ? DefaultMaxResults : maxResults, 1, MaxResultsCap);

    internal static double NormalizeMinLinePercentage(double minLinePercentage) =>
        double.IsNaN(minLinePercentage) || double.IsInfinity(minLinePercentage)
            ? DefaultMinLinePercentage
            : Math.Clamp(minLinePercentage, MinLinePercentage, MaxLinePercentage);

    internal static string NormalizeScopeType(string? scopeType) =>
        string.IsNullOrWhiteSpace(scopeType) ? DefaultScopeType : scopeType.Trim().ToLowerInvariant();

    internal static bool IsValidScopeType(string? scopeType) =>
        NormalizeScopeType(scopeType) is "production" or "tests" or "all";

    /// <summary>
    /// Baut den vollstaendigen Hotspot-Report fuer <paramref name="solution"/> — Text (Markdown-
    /// Tabellen) plus <see cref="HotspotEntry"/>-Liste fuer <c>StructuredContent</c>. Ist
    /// <paramref name="scopeFilter"/> gesetzt, aber matched
    /// keine Datei, wird eine explizite "Keine Dateien im Scope"-Meldung geliefert statt der sonst
    /// irrefuehrenden "alles gruen"-Aussage (Entries dann leer).
    /// </summary>
    internal static (string Text, IReadOnlyList<HotspotEntry> Entries) BuildHotspots(
        Solution solution, int maxLineCount, string? scopeFilter)
    {
        var report = BuildHotspots(
            solution,
            new HotspotScanOptions(
                maxLineCount,
                scopeFilter,
                DefaultMaxResults,
                DefaultMinLinePercentage,
                DefaultScopeType));
        return (report.Text, report.Entries);
    }

    internal static HotspotScanResult BuildHotspots(
        Solution solution,
        HotspotScanOptions options)
    {
        var effectiveMaxResults = NormalizeMaxResults(options.MaxResults);
        var effectiveMinLinePercentage = NormalizeMinLinePercentage(options.MinLinePercentage);
        var maxLineCount = options.MaxLineCount;
        var scopeFilter = options.ScopeFilter;
        var scopeType = NormalizeScopeType(options.ScopeType);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var files = CollectFiles(solution, solutionDir, scopeFilter, scopeType);

        if (files.Count == 0 && !string.IsNullOrWhiteSpace(scopeFilter))
        {
            return new HotspotScanResult(
                $"Keine Dateien im Scope (Filter: '{scopeFilter}') — Filter pruefen.",
                Array.Empty<HotspotEntry>(),
                0,
                0,
                false,
                effectiveMaxResults,
                effectiveMinLinePercentage,
                scopeType);
        }

        var candidates = files
            .Where(f => GetUtilization(f, maxLineCount) >= effectiveMinLinePercentage)
            .OrderByDescending(f => f.Lines)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shown = candidates.Take(effectiveMaxResults).ToList();
        var critical = shown.Where(f => GetUtilization(f, maxLineCount) >= CriticalThreshold * 100).ToList();
        var warning = shown.Where(f => GetUtilization(f, maxLineCount) < CriticalThreshold * 100).ToList();

        var text = FormatReport(
            new HotspotReportData(
                files,
                critical,
                warning,
                candidates.Count,
                shown.Count,
                maxLineCount,
                scopeFilter,
                effectiveMinLinePercentage,
                scopeType));
        var entries = BuildEntries(shown, maxLineCount);
        return new HotspotScanResult(
            text,
            entries,
            candidates.Count,
            shown.Count,
            candidates.Count > shown.Count,
            effectiveMaxResults,
            effectiveMinLinePercentage,
            scopeType);
    }

    /// <summary>
    /// Baut <see cref="HotspotEntry"/>s nur fuer <paramref name="critical"/>/<paramref name="warning"/>
    /// — dieselben Listen, die auch <see cref="FormatReport"/> fuer die Text-Sektionen verwendet,
    /// damit Text und StructuredContent nie in der Kategorisierung auseinanderdriften. Dateien im
    /// gruenen Bereich ("ok") werden bewusst NICHT aufgenommen: fruehere Fassung listete alle
    /// gescannten Dateien (auch "ok") in StructuredContent, was bei einer grossen Solution die
    /// Antwort auf mehrere zehntausend Zeichen aufblaehte und den Client-Token-Guard sprengte —
    /// genau das Gegenteil vom Zweck eines Hotspot-Reports (nur die Dateien nahe/ueber dem Limit).
    /// </summary>
    private static IReadOnlyList<HotspotEntry> BuildEntries(
        IReadOnlyList<HotspotFileInfo> files,
        int maxLineCount)
    {
        return files
            .Select(f => BuildEntry(
                f,
                maxLineCount,
                GetUtilization(f, maxLineCount) >= CriticalThreshold * 100 ? "critical" : "warning"))
            .OrderByDescending(e => e.Lines)
            .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HotspotEntry BuildEntry(HotspotFileInfo file, int maxLineCount, string category) =>
        new(file.RelativePath, file.Lines, Math.Round(GetUtilization(file, maxLineCount), 1), category);

    private static double GetUtilization(HotspotFileInfo file, int maxLineCount) =>
        (double)file.Lines / maxLineCount * 100;

    private static List<HotspotFileInfo> CollectFiles(
        Solution solution,
        string solutionDir,
        string? scopeFilter,
        string scopeType)
    {
        var result = new List<HotspotFileInfo>();

        foreach (var walked in SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter))
        {
            if (!MatchesScopeType(walked, scopeType)) continue;
            var lines = SolutionFileWalker.TryReadAllLines(walked)?.Length;
            if (lines is null) continue;

            result.Add(new HotspotFileInfo(walked.RelativePath, lines.Value));
        }

        return result;
    }

    private static bool MatchesScopeType(WalkedFile file, string scopeType)
    {
        if (scopeType == "all") return true;

        var isTest = TestDetector.IsTestProject(file.Document.Project)
            || TestDetector.IsTestFile(file.RelativePath);
        return scopeType == "tests" ? isTest : !isTest;
    }

    private static string FormatReport(HotspotReportData report)
    {
        var sb = new StringBuilder();
        var scopeSuffix = string.IsNullOrWhiteSpace(report.ScopeFilter) ? "" : $" | Scope-Filter: '{report.ScopeFilter}'";
        sb.AppendLine($"Gescannt: {report.Files.Count} .cs-Dateien | MaxLineCount: {report.MaxLineCount} | Scope-Typ: '{report.ScopeType}'{scopeSuffix}");
        sb.AppendLine();

        HotspotTableFormatter.AppendSection(sb, "Kritische Dateien (>=95% des Limits)", report.Critical.Select(f => (f.RelativePath, f.Lines)), report.MaxLineCount);
        HotspotTableFormatter.AppendSection(sb, $"Warnungs-Dateien (>= {FormatPercentage(report.MinLinePercentage)}% des Limits)", report.Warning.Select(f => (f.RelativePath, f.Lines)), report.MaxLineCount);

        if (report.TotalHotspots == 0)
        {
            sb.AppendLine("## Alle Dateien im gruenen Bereich");
            sb.AppendLine();
            sb.AppendLine($"Keine Datei erreicht {FormatPercentage(report.MinLinePercentage)}% des Limits.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"## Alle anderen Dateien: {report.Files.Count - report.TotalHotspots} Dateien im gruenen Bereich");
        }

        if (report.TotalHotspots > report.ShownHotspots)
        {
            sb.AppendLine();
            sb.AppendLine($"[{report.TotalHotspots} Hotspots gesamt, {report.ShownHotspots} gezeigt — maxResults erhöhen]");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatPercentage(double percentage) =>
        percentage % 1 == 0
            ? percentage.ToString("0", CultureInfo.InvariantCulture)
            : percentage.ToString("0.0", CultureInfo.InvariantCulture);

    private sealed record HotspotReportData(
        IReadOnlyList<HotspotFileInfo> Files,
        IReadOnlyList<HotspotFileInfo> Critical,
        IReadOnlyList<HotspotFileInfo> Warning,
        int TotalHotspots,
        int ShownHotspots,
        int MaxLineCount,
        string? ScopeFilter,
        double MinLinePercentage,
        string ScopeType);

    private sealed record HotspotFileInfo(string RelativePath, int Lines);
}

internal sealed record HotspotScanOptions(
    int MaxLineCount,
    string? ScopeFilter,
    int MaxResults,
    double MinLinePercentage,
    string? ScopeType = GetHotspotsScanner.DefaultScopeType);

internal sealed record HotspotScanResult(
    string Text,
    IReadOnlyList<HotspotEntry> Entries,
    int TotalHotspots,
    int ShownHotspots,
    bool Truncated,
    int MaxResults,
    double MinLinePercentage,
    string ScopeType);

/// <summary>
/// StructuredContent-Eintrag fuer <c>get_hotspots</c> — ein Objekt je Datei mit Pfad, Zeilen
/// und Auslastung, nur fuer <see cref="Category"/> <c>"critical"</c> (>=95%) oder <c>"warning"</c>
/// (>= dem angeforderten Minimum) — Dateien im gruenen Bereich tauchen bewusst nicht auf (siehe
/// <see cref="GetHotspotsScanner.BuildEntries"/>).
/// </summary>
internal sealed record HotspotEntry(string RelativePath, int Lines, double UtilizationPercent, string Category);

internal sealed record HotspotsPayload(
    IReadOnlyList<HotspotEntry> Hotspots,
    int TotalHotspots,
    int ShownHotspots,
    bool Truncated,
    int MaxResults,
    double MinLinePercentage,
    string ScopeType);
