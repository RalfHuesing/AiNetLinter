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
    internal static string Render(MetricsTreeNode root, int topN, bool sortDescending) =>
        Render(root, new MetricsTreeRenderOptions(topN, sortDescending));

    internal static string Render(MetricsTreeNode root, MetricsTreeRenderOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(FormatNode(root, options.IncludeHandoffStatus));
        RenderChildren(sb, root, "", options);
        return sb.ToString().TrimEnd();
    }

    private static void RenderChildren(
        StringBuilder sb, MetricsTreeNode owner, string prefix,
        MetricsTreeRenderOptions options)
    {
        var children = owner.Children;
        var sorted = options.SortDescending
            ? children.OrderByDescending(c => c.SortValue).ThenBy(c => c.RelativePath, System.StringComparer.OrdinalIgnoreCase).ToList()
            : children.OrderBy(c => c.SortValue).ThenBy(c => c.RelativePath, System.StringComparer.OrdinalIgnoreCase).ToList();
        var visible = sorted.Take(options.TopN).ToList();

        for (var i = 0; i < visible.Count; i++)
        {
            var isLast = i == visible.Count - 1 && visible.Count == sorted.Count;
            var branch = isLast ? "└── " : "├── ";
            sb.AppendLine($"{prefix}{branch}{FormatNode(visible[i], options.IncludeHandoffStatus)}");
            var childPrefix = prefix + (isLast ? "    " : "│   ");
            RenderChildren(sb, visible[i], childPrefix, options);
        }

        var hiddenCount = owner.HiddenChildCount + Math.Max(0, sorted.Count - visible.Count);
        if (hiddenCount > 0)
        {
            sb.AppendLine($"{prefix}└── ... und {hiddenCount} weitere");
        }
    }

    private static string FormatNode(MetricsTreeNode node, bool includeHandoffStatus)
    {
        return $"{node.Name} — {node.DisplayLine}" +
            (includeHandoffStatus ? "; handoff: not_applicable" : string.Empty);
    }
}

internal sealed record MetricsTreeRenderOptions(
    int TopN,
    bool SortDescending,
    bool IncludeHandoffStatus = false);
