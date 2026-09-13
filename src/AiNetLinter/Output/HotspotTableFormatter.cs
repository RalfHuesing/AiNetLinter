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
        int maxLineCount) =>
        AppendSection(sb, heading, files, new HotspotTableOptions(maxLineCount));

    internal static void AppendSection(
        StringBuilder sb,
        string heading,
        IEnumerable<(string RelativePath, int Lines)> files,
        HotspotTableOptions options)
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
                if (options.IncludeHandoffStatus) t.AddColumn("Handoff");

                foreach (var (relativePath, lines) in list
                    .OrderByDescending(x => x.Lines)
                    .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    var pct = (double)lines / options.MaxLineCount * 100;
                    var remaining = options.MaxLineCount - lines;
                    var cells = options.IncludeHandoffStatus
                        ? new object?[] { relativePath, lines, $"{pct:F0} %", $"{remaining} Zeilen", "not_applicable" }
                        : [relativePath, lines, $"{pct:F0} %", $"{remaining} Zeilen"];
                    t.AddRow(cells);
                }
            });
        }

        mb.AppendTo(sb);
        sb.AppendLine();
    }
}

internal sealed record HotspotTableOptions(
    int MaxLineCount,
    bool IncludeHandoffStatus = false);
