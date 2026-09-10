#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>
/// Modus-agnostischer Baum-Knoten fuer <c>metrics_tree</c>. Kennt weder <see cref="Microsoft.CodeAnalysis.Solution"/>
/// noch die Modus-Herkunft der Werte — <see cref="DisplayLine"/> ist bereits vorformatiert, damit
/// derselbe Typ sowohl von den Datei-Walk-Modi als auch von den Roslyn-Modi produziert werden
/// kann.
/// </summary>
internal sealed record MetricsTreeNode(
    string Name,
    string RelativePath,
    int FileCount,
    double SortValue,
    string DisplayLine,
    IReadOnlyList<MetricsTreeNode> Children,
    bool Handoff = false,
    string? Id = null,
    string? TargetPath = null,
    string? Snapshot = null,
    string? SymbolKind = null,
    IReadOnlyList<string>? AllowedFollowUpTools = null,
    // Anzahl der direkten Kinder, die in dieser Projektion nicht sichtbar sind.
    // Verschachtelte Auslassungen bleiben am jeweiligen Elternknoten und werden
    // nicht in diesen Wert eingerechnet.
    int HiddenChildCount = 0);

internal static class MetricsTreeProjection
{
    internal static MetricsTreeNode Project(
        MetricsTreeNode node,
        int topN,
        bool sortDescending)
    {
        var sorted = sortDescending
            ? node.Children.OrderByDescending(c => c.SortValue).ThenBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase).ToList()
            : node.Children.OrderBy(c => c.SortValue).ThenBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var visible = sorted.Take(topN)
            .Select(child => Project(child, topN, sortDescending))
            .ToList();
        return node with
        {
            Children = visible,
            HiddenChildCount = sorted.Count - visible.Count,
        };
    }

    internal static int CountNodes(MetricsTreeNode node) =>
        1 + node.Children.Sum(CountNodes) + node.HiddenChildCount;

    internal static int CountVisibleNodes(MetricsTreeNode node) =>
        1 + node.Children.Sum(CountVisibleNodes);
}

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
        sb.AppendLine(FormatNode(root));
        RenderChildren(sb, root, "", topN, sortDescending);
        return sb.ToString().TrimEnd();
    }

    private static void RenderChildren(
        StringBuilder sb, MetricsTreeNode owner, string prefix,
        int topN, bool sortDescending)
    {
        var children = owner.Children;
        var sorted = sortDescending
            ? children.OrderByDescending(c => c.SortValue).ThenBy(c => c.RelativePath, System.StringComparer.OrdinalIgnoreCase).ToList()
            : children.OrderBy(c => c.SortValue).ThenBy(c => c.RelativePath, System.StringComparer.OrdinalIgnoreCase).ToList();
        var visible = sorted.Take(topN).ToList();

        for (var i = 0; i < visible.Count; i++)
        {
            var isLast = i == visible.Count - 1 && visible.Count == sorted.Count;
            AppendNodeLine(sb, visible[i], prefix, isLast);
            var childPrefix = prefix + (isLast ? "    " : "│   ");
            RenderChildren(sb, visible[i], childPrefix, topN, sortDescending);
        }

        var hiddenCount = owner.HiddenChildCount + Math.Max(0, sorted.Count - visible.Count);
        if (hiddenCount > 0)
        {
            sb.AppendLine($"{prefix}└── ... und {hiddenCount} weitere");
        }
    }

    private static void AppendNodeLine(StringBuilder sb, MetricsTreeNode node, string prefix, bool isLast)
    {
        var branch = isLast ? "└── " : "├── ";
        sb.AppendLine($"{prefix}{branch}{FormatNode(node)}");
    }

    private static string FormatNode(MetricsTreeNode node)
    {
        var handoff = node.Id is not null
            ? $"handoff=true; id=`{node.Id}`"
            : node.SymbolKind is not null
                ? "handoff=false; followUpTools=[]"
                : string.Empty;
        return string.IsNullOrEmpty(handoff)
            ? $"{node.Name} — {node.DisplayLine}"
            : $"{node.Name} — {node.DisplayLine} [{handoff}]";
    }
}
