#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>
/// Modus-agnostischer Baum-Knoten fuer <c>metrics_tree</c>. Kennt weder <see cref="Microsoft.CodeAnalysis.Solution"/>
/// noch die Modus-Herkunft der Werte — <see cref="DisplayLine"/> ist bereits vorformatiert, damit
/// derselbe Typ sowohl von den Datei-Walk-Modi (EPIC-01) als auch von den Roslyn-Modi (EPIC-02)
/// produziert werden kann.
/// </summary>
internal sealed record MetricsTreeNode(
    string Name,
    string RelativePath,
    int FileCount,
    double SortValue,
    string DisplayLine,
    IReadOnlyList<MetricsTreeNode> Children);

/// <summary>
/// Rein formatierender ASCII-Tree-Renderer ueber einer bereits aggregierten
/// <see cref="MetricsTreeNode"/>-Baumstruktur — kennt keine Solution/Modus-Herkunft. Top-N pro Ebene,
/// Rest wird als Zahl in einer "... und N weitere"-Zeile zusammengefasst statt stillschweigend
/// weggelassen.
/// </summary>
internal static class MetricsTreeRenderer
{
    internal static string Render(MetricsTreeNode root, int topN, bool sortDescending)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{root.Name} — {root.DisplayLine}");
        RenderChildren(sb, root.Children, "", topN, sortDescending);
        return sb.ToString().TrimEnd();
    }

    private static void RenderChildren(
        StringBuilder sb, IReadOnlyList<MetricsTreeNode> children, string prefix,
        int topN, bool sortDescending)
    {
        var sorted = sortDescending
            ? children.OrderByDescending(c => c.SortValue).ToList()
            : children.OrderBy(c => c.SortValue).ToList();
        var visible = sorted.Take(topN).ToList();

        for (var i = 0; i < visible.Count; i++)
        {
            var isLast = i == visible.Count - 1 && visible.Count == sorted.Count;
            AppendNodeLine(sb, visible[i], prefix, isLast);
            var childPrefix = prefix + (isLast ? "    " : "│   ");
            RenderChildren(sb, visible[i].Children, childPrefix, topN, sortDescending);
        }

        if (sorted.Count > visible.Count)
        {
            sb.AppendLine($"{prefix}└── ... und {sorted.Count - visible.Count} weitere");
        }
    }

    private static void AppendNodeLine(StringBuilder sb, MetricsTreeNode node, string prefix, bool isLast)
    {
        var branch = isLast ? "└── " : "├── ";
        sb.AppendLine($"{prefix}{branch}{node.Name} — {node.DisplayLine}");
    }
}
