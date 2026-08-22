#nullable enable

using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Tools.MetricsTree;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>
/// Rendert dieselbe <see cref="MetricsTreeNode"/>-Baumstruktur, die <see cref="CallGraphTreeBuilder.BuildTreeAsync"/>
/// liefert, als Mermaid-<c>flowchart TD</c>-Block fuer <c>get_call_tree</c> mit <c>format=mermaid</c>.
/// Eigenstaendiger Renderer statt einer Erweiterung von <see cref="MetricsTreeRenderer"/>, weil das
/// Zielformat (eindeutige Knoten-IDs + gerichtete Kanten statt Einrueckung/Praefix-Zeichen)
/// grundverschieden ist — eine Vereinheitlichung wuerde beide Renderer unnoetig verkomplizieren.
/// Wendet dieselbe Top-N-pro-Ebene-Kappung wie <see cref="MetricsTreeRenderer"/> an (ein
/// zusaetzlicher "... und N weitere"-Knoten statt stillschweigendem Weglassen).
/// </summary>
internal static class CallTreeMermaidRenderer
{
    internal static string Render(MetricsTreeNode root, int topN)
    {
        var state = new RenderState(topN);
        state.Sb.AppendLine("flowchart TD");
        var rootId = NextId(state);
        AppendNodeDeclaration(state.Sb, rootId, root);
        AppendChildren(state, root, rootId);
        return state.Sb.ToString().TrimEnd();
    }

    private static void AppendChildren(RenderState state, MetricsTreeNode node, string parentId)
    {
        var visible = node.Children.Take(state.TopN).ToList();
        foreach (var child in visible)
        {
            var childId = NextId(state);
            AppendNodeDeclaration(state.Sb, childId, child);
            state.Sb.AppendLine($"    {parentId} --> {childId}");
            AppendChildren(state, child, childId);
        }

        if (node.Children.Count > visible.Count)
        {
            AppendOverflowNode(state, parentId, node.Children.Count - visible.Count);
        }
    }

    private static void AppendOverflowNode(RenderState state, string parentId, int remaining)
    {
        var overflowId = NextId(state);
        state.Sb.AppendLine($"    {overflowId}[\"... und {remaining} weitere\"]");
        state.Sb.AppendLine($"    {parentId} --> {overflowId}");
    }

    private static void AppendNodeDeclaration(StringBuilder sb, string id, MetricsTreeNode node)
    {
        sb.AppendLine($"    {id}[\"{EscapeLabel(FormatLabel(node))}\"]");
    }

    private static string FormatLabel(MetricsTreeNode node) => $"{node.Name} — {node.DisplayLine}";

    // Mermaid-Labels in eckigen Klammern vertragen kein Anfuehrungszeichen/keinen Zeilenumbruch —
    // ersetzen statt escapen, damit der Flowchart-Block syntaktisch gueltig bleibt.
    private static string EscapeLabel(string label) => label.Replace("\"", "'").Replace("\n", " ");

    private static string NextId(RenderState state) => $"n{state.NextNodeIndex++}";

    /// <summary>Veraenderlicher Render-Zustand — buendelt StringBuilder, topN und den ID-Zaehler,
    /// damit die rekursiven Render-Methoden innerhalb von <c>MaxMethodParameterCount</c> bleiben.</summary>
    private sealed class RenderState
    {
        internal RenderState(int topN)
        {
            TopN = topN;
        }

        internal StringBuilder Sb { get; } = new();
        internal int TopN { get; }
        internal int NextNodeIndex { get; set; }
    }
}
