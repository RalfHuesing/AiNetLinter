#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Output;

/// <summary>
/// Einheitliche Formatierung von Hotspot-Dateitabellen mit Zeilenanzahl, Auslastung und Restkapazität.
/// </summary>
internal static class HotspotTableFormatter
{
    internal static void AppendSection(
        StringBuilder sb,
        string heading,
        IEnumerable<(string RelativePath, int Lines)> files,
        int maxLineCount)
    {
        var mb = new MarkdownBuilder();
        mb.Heading(2, heading).BlankLine();

        var list = files.ToList();
        if (list.Count == 0)
        {
            mb.Line("Keine.");
        }
        else
        {
            mb.Table(t =>
            {
                t.AddColumn("Datei")
                 .AddColumn("Zeilen", ColumnAlign.Right)
                 .AddColumn("Auslastung", ColumnAlign.Right)
                 .AddColumn("Verbleibend", ColumnAlign.Right);

                foreach (var (relativePath, lines) in list
                    .OrderByDescending(x => x.Lines)
                    .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    var pct = (double)lines / maxLineCount * 100;
                    var remaining = maxLineCount - lines;
                    t.AddRow(relativePath, lines, $"{pct:F0} %", $"{remaining} Zeilen");
                }
            });
        }

        mb.AppendTo(sb);
        sb.AppendLine();
    }
}
