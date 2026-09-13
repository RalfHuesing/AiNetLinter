#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Handoffs;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>Rendert die flache Nodes/Edges-Domain in die beiden Textprojektionen.</summary>
internal static class CallGraphTextRenderer
{
    internal static string RenderAscii(CallGraphPayload graph)
    {
        var nodes = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var root = nodes[graph.RootNodeId];
        var builder = new StringBuilder($"[{root.NodeId}] {FormatNode(root)}");
        foreach (var edge in graph.Edges)
        {
            if (!nodes.TryGetValue(edge.FromNodeId, out var from)
                || !nodes.TryGetValue(edge.ToNodeId, out var to)) continue;

            builder.Append("\n├── ");
            builder.Append(FormatEdge(from, to, edge));
        }

        if (graph.HiddenEdgeCount > 0)
        {
            builder.Append($"\n└── ... und {graph.HiddenEdgeCount} weitere");
        }

        AppendNodeHandoffs(builder, graph.Nodes);
        AppendMethodHints(builder, graph.MethodHints);
        return builder.ToString();
    }

    internal static string RenderMermaid(CallGraphPayload graph)
    {
        var builder = new StringBuilder("flowchart TD");
        foreach (var node in graph.Nodes)
        {
            builder.Append("\n    ");
            builder.Append(node.NodeId);
            builder.Append("[\"");
            builder.Append(EscapeLabel(FormatNode(node)));
            builder.Append("\"]");
        }

        foreach (var edge in graph.Edges)
        {
            builder.Append("\n    ");
            builder.Append(edge.FromNodeId);
            builder.Append(" --> ");
            builder.Append(edge.ToNodeId);
        }

        if (graph.HiddenEdgeCount > 0)
        {
            builder.Append("\n    overflow[\"... und ");
            builder.Append(graph.HiddenEdgeCount);
            builder.Append(" weitere\"]\n    ");
            builder.Append(graph.RootNodeId);
            builder.Append(" --> overflow");
        }

        AppendNodeHandoffs(builder, graph.Nodes, mermaidComment: true);
        AppendMethodHints(builder, graph.MethodHints);
        return builder.ToString();
    }

    private static string FormatEdge(CallGraphNode from, CallGraphNode to, CallGraphEdge edge)
    {
        var site = edge.CallSites.FirstOrDefault();
        var location = site is null ? string.Empty : $" — {site.FilePath}:{site.Line}";
        var dispatch = string.IsNullOrWhiteSpace(edge.DispatchKind)
            ? string.Empty
            : $" [{edge.DispatchKind}]";
        return $"[{from.NodeId}] {from.Name} -> [{to.NodeId}] {to.Name}{location}{dispatch}";
    }

    private static string FormatNode(CallGraphNode node) =>
        string.IsNullOrWhiteSpace(node.DisplayLine)
            ? node.Name
            : $"{node.Name} — {node.DisplayLine}";

    private static void AppendNodeHandoffs(
        StringBuilder builder,
        IReadOnlyList<CallGraphNode> nodes,
        bool mermaidComment = false)
    {
        var navigableNodes = nodes
            .Where(node => SymbolHandoffIdentifier.HasWirePrefix(node.SymbolId))
            .ToList();
        if (navigableNodes.Count == 0) return;

        if (mermaidComment)
        {
            foreach (var node in navigableNodes)
            {
                builder.Append("\n    %% handoffId: ");
                builder.Append(node.NodeId);
                builder.Append(" = ");
                builder.Append(HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(node.SymbolId));
            }
            return;
        }

        builder.Append("\n\nKnoten-Handoffs:");
        foreach (var node in navigableNodes)
        {
            builder.Append("\n- ");
            builder.Append(node.NodeId);
            builder.Append(" (");
            builder.Append(node.Name);
            builder.Append("): handoffId: `");
            builder.Append(HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(node.SymbolId));
            builder.Append('`');
        }
    }

    private static void AppendMethodHints(StringBuilder builder, IReadOnlyList<CallGraphMethodHint>? hints)
    {
        if (hints is not { Count: > 0 }) return;
        builder.Append("\n\nHinweis: passende Methoden des Typs: ");
        builder.Append(string.Join(", ", hints.Select(hint => hint.Name)));
    }

    private static string EscapeLabel(string label) =>
        label.Replace("\"", "'").Replace("\r", " ").Replace("\n", " ");
}
