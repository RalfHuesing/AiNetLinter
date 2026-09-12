#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>Begrenzt den gerenderten Text auf ganze Graph-Einheiten.</summary>
internal static partial class CallGraphResponseBudget
{
    internal static CallToolResult CreateResult(CallGraphResponseRequest request)
    {
        var graph = request.Graph;
        var format = request.Format;
        var maxResponseBytes = request.MaxResponseBytes;
        var edges = graph.Edges.ToList();
        var hints = graph.MethodHints?.ToList() ?? [];
        var budgetTruncated = false;

        while (Encoding.UTF8.GetByteCount(
                   CreateText(graph, format, edges, hints, budgetTruncated, maxResponseBytes)) > maxResponseBytes)
        {
            budgetTruncated = true;
            if (hints.Count > 0)
            {
                hints.RemoveAt(hints.Count - 1);
                continue;
            }

            if (edges.Count == 0) break;
            edges.RemoveAt(edges.Count - 1);
        }

        var text = CreateText(graph, format, edges, hints, budgetTruncated, maxResponseBytes);
        var minimumResponseBytes = Encoding.UTF8.GetByteCount(text);
        if (minimumResponseBytes > maxResponseBytes)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ResponseBudgetTooSmall,
                "maxResponseBytes ist zu klein, um den festen Call-Graph-Envelope vollständig auszugeben.",
                new McpErrorParameters(
                    Hint: "maxResponseBytes erhöhen oder symbolIdentifier/scopeType verfeinern.",
                    FieldPath: "$.maxResponseBytes",
                    RequestedBytes: maxResponseBytes,
                    MinimumResponseBytes: minimumResponseBytes));
        }

        return McpToolResults.Text(text);
    }

    private static string CreateText(
        CallGraphPayload graph,
        string? format,
        IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints,
        bool budgetTruncated,
        int maxResponseBytes)
    {
        var projected = graph with
        {
            Nodes = NodesForEdges(graph, edges),
            Edges = edges,
            MethodHints = hints,
        };
        var text = GetCallTreeTool.RenderGraph(projected, format);
        if (graph.TopNTruncated)
        {
            text += "\n\n[Graph trunkiert — topN erhoehen fuer einen vollstaendigeren Graphen]";
        }
        if (graph.HardCapTruncated)
        {
            text += $"\n\n[Graph trunkiert — hard-cap {CallGraphTreeBuilder.MaxCallTreeNodes} Knoten erreicht]";
        }

        return budgetTruncated
            ? text + $"\n\n[Antwort wegen maxResponseBytes={maxResponseBytes} begrenzt — ganze Graph-Kanten wurden berücksichtigt]"
            : text;
    }

    private static IReadOnlyList<CallGraphNode> NodesForEdges(
        CallGraphPayload graph,
        IReadOnlyList<CallGraphEdge> edges)
    {
        var ids = new HashSet<string>(System.StringComparer.Ordinal) { graph.RootNodeId };
        foreach (var edge in edges)
        {
            ids.Add(edge.FromNodeId);
            ids.Add(edge.ToNodeId);
        }

        return graph.Nodes.Where(node => ids.Contains(node.NodeId)).ToList();
    }

}
