#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Erzeugt eine Hotspot Map: Dateien die sich ihrem konfigurierten Limit nähern.
/// Proaktives Drift-Signal — sichtbar bevor ein Regelverstoß entsteht.
/// </summary>
internal static class HotspotMapBuilder
{
    private const double WarnThreshold     = 0.80;
    private const double CriticalThreshold = 0.95;

    internal static int Build(string targetPath, int maxLineCount, ILintConsole c)
    {
        var root = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath) ?? targetPath;
        if (!Directory.Exists(root))
        {
            c.WriteError($"[ERROR]: Pfad '{root}' existiert nicht.");
            return 1;
        }

        var files = StructureMapBuilder.CollectFileInfos(root);
        var critical = files.Where(f => (double)f.Lines / maxLineCount >= CriticalThreshold).ToList();
        var warning  = files.Where(f => (double)f.Lines / maxLineCount is >= WarnThreshold and < CriticalThreshold).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Hotspot Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien | MaxLineCount: {maxLineCount} | Pfad: {root.Replace('\\', '/')}");
        sb.AppendLine();

        AppendSection(sb, "🔴 Kritische Dateien (>95% des Limits)", critical, maxLineCount);
        AppendSection(sb, "⚠ Warnungs-Dateien (>80% des Limits)", warning, maxLineCount);

        if (critical.Count == 0 && warning.Count == 0)
        {
            sb.AppendLine("## ✓ Alle Dateien im grünen Bereich");
            sb.AppendLine();
            sb.AppendLine($"Keine Datei überschreitet 80% des Limits ({(int)(maxLineCount * WarnThreshold)} Zeilen).");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"## Alle anderen Dateien: {files.Count - critical.Count - warning.Count} Dateien im grünen Bereich");
        }

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }

    private static void AppendSection(
        StringBuilder sb,
        string heading,
        System.Collections.Generic.IReadOnlyList<StructureFileInfo> files,
        int maxLineCount)
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
}
