#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Output;

/// <summary>
/// Formatiert eine Hotspot-Markdown-Tabelle (Datei/Zeilen/Auslastung/Verbleibend) fuer eine
/// Datei-Kategorie (kritisch/warnend). Gemeinsam genutzt von <see cref="AiNetLinter.Maps.HotspotMapBuilder"/>
/// (CLI-Map, Einmal-Filesystem-Scan) und <see cref="AiNetLinter.Mcp.Tools.FileStructure.GetHotspotsScanner"/>
/// (MCP, resident gehaltene Solution) — beide bauen ihre Datei-Liste unterschiedlich auf, die reine
/// Formatierung ist aber identisch, daher hier ueber ein generisches Tupel entkoppelt.
/// </summary>
internal static class HotspotSectionFormatter
{
    internal static void AppendSection(
        StringBuilder sb,
        string heading,
        IReadOnlyList<(string RelativePath, int Lines)> files,
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
