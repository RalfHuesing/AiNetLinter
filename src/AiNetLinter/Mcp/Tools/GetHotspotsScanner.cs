#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Reine Scan-/Formatierungslogik fuer <see cref="GetHotspotsTool"/> — in eine eigene Datei
/// ausgelagert, damit <see cref="GetHotspotsTool"/>s eigener <c>AIContextFootprint</c> (siehe
/// <c> klein bleibt.
/// Keine Abhaengigkeit von <see cref="McpCodeGraphServer"/> — direkt unit-testbar. Liefert dieselbe
/// Kennzahl wie <see cref="AiNetLinter.Maps.HotspotMapBuilder"/> (CLI-Map-Typ <c>--map hotspots</c>),
/// aber gegen die resident gehaltene <see cref="Solution"/> statt eines Einmal-Filesystem-Scans, damit
/// z. B. Test-Fixtures im selben Verzeichnisbaum nicht faelschlich mitgezaehlt werden 
/// JIT-Kontext). Die zwei Schwellwert-Konstanten sind bewusst aus <see cref="AiNetLinter.Maps.HotspotMapBuilder"/>
/// dupliziert (dessen Formatierungs-Methoden sind <c>private</c>, eine Abhaengigkeit dorthin wuerde
/// keinen echten Wiederverwendungs-Gewinn bringen).
/// </summary>
internal static class GetHotspotsScanner
{
    private const double WarnThreshold = 0.80;
    private const double CriticalThreshold = 0.95;

    /// <summary>
    /// Baut den vollstaendigen Hotspot-Report fuer <paramref name="solution"/> als Text. Ist
    /// <paramref name="scopeFilter"/> gesetzt, aber matched keine Datei, wird eine explizite
    /// "Keine Dateien im Scope"-Meldung geliefert statt der sonst irrefuehrenden "alles gruen"-Aussage.
    /// </summary>
    internal static string BuildHotspotsText(Solution solution, int maxLineCount, string? scopeFilter)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var files = CollectFiles(solution, solutionDir, scopeFilter);

        if (files.Count == 0 && !string.IsNullOrWhiteSpace(scopeFilter))
        {
            return $"Keine Dateien im Scope (Filter: '{scopeFilter}') — Filter pruefen.";
        }

        var critical = files.Where(f => (double)f.Lines / maxLineCount >= CriticalThreshold).ToList();
        var warning = files.Where(f => (double)f.Lines / maxLineCount is >= WarnThreshold and < CriticalThreshold).ToList();

        return FormatReport(files, critical, warning, maxLineCount, scopeFilter);
    }

    private static List<HotspotFileInfo> CollectFiles(Solution solution, string solutionDir, string? scopeFilter)
    {
        var result = new List<HotspotFileInfo>();

        foreach (var walked in SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter))
        {
            var lines = SolutionFileWalker.TryReadAllLines(walked.AbsolutePath)?.Length;
            if (lines is null) continue;

            result.Add(new HotspotFileInfo(walked.RelativePath, lines.Value));
        }

        return result;
    }

    private static string FormatReport(
        IReadOnlyList<HotspotFileInfo> files,
        IReadOnlyList<HotspotFileInfo> critical,
        IReadOnlyList<HotspotFileInfo> warning,
        int maxLineCount,
        string? scopeFilter)
    {
        var sb = new StringBuilder();
        var scopeSuffix = string.IsNullOrWhiteSpace(scopeFilter) ? "" : $" | Scope-Filter: '{scopeFilter}'";
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien | MaxLineCount: {maxLineCount}{scopeSuffix}");
        sb.AppendLine();

        AppendSection(sb, "Kritische Dateien (>=95% des Limits)", critical, maxLineCount);
        AppendSection(sb, "Warnungs-Dateien (>=80% des Limits)", warning, maxLineCount);

        if (critical.Count == 0 && warning.Count == 0)
        {
            sb.AppendLine("## Alle Dateien im gruenen Bereich");
            sb.AppendLine();
            sb.AppendLine($"Keine Datei ueberschreitet 80% des Limits ({(int)(maxLineCount * WarnThreshold)} Zeilen).");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"## Alle anderen Dateien: {files.Count - critical.Count - warning.Count} Dateien im gruenen Bereich");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendSection(
        StringBuilder sb, string heading, IReadOnlyList<HotspotFileInfo> files, int maxLineCount)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine();

        if (files.Count == 0)
        {
            sb.AppendLine("Keine.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Datei | Zeilen | Auslastung | Verbleibend |");
        sb.AppendLine("|:---|---:|---:|---:|");
        foreach (var f in files.OrderByDescending(x => x.Lines).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var pct = (double)f.Lines / maxLineCount * 100;
            var remaining = maxLineCount - f.Lines;
            sb.AppendLine($"| {f.RelativePath} | {f.Lines} | {pct:F0} % | {remaining} Zeilen |");
        }
        sb.AppendLine();
    }

    private sealed record HotspotFileInfo(string RelativePath, int Lines);
}
