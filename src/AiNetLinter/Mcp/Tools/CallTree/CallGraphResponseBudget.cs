#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>Begrenzt Text und StructuredContent gemeinsam auf ganze Graph-Einheiten.</summary>
internal static partial class CallGraphResponseBudget
{
    private sealed record CallGraphPayloadRequest(
        CallGraphPayload Graph,
        IReadOnlyList<CallGraphEdge> Edges,
        IReadOnlyList<CallGraphMethodHint> Hints,
        string Direction,
        int RequestedDepth,
        int EffectiveDepth,
        int TopN,
        McpScopeType ScopeType,
        bool IncludeGenerated,
        bool BudgetTruncated,
        int OriginalNodes,
        int OriginalEdges,
        AssemblyNavigationSummary? AssemblyNavigation);

    internal static CallToolResult CreateResult(CallGraphResponseRequest request)
    {
        var graph = request.Graph;
        var format = request.Format;
        var direction = request.Direction;
        var requestedDepth = request.RequestedDepth;
        var effectiveDepth = request.EffectiveDepth;
        var topN = request.TopN;
        var scopeType = request.ScopeType;
        var includeGenerated = request.IncludeGenerated;
        var maxResponseBytes = request.MaxResponseBytes;
        var originalNodes = graph.Nodes.Count;
        var originalEdges = graph.Edges.Count;
        var edges = graph.Edges.ToList();
        var hints = graph.MethodHints?.ToList() ?? [];
        var budgetTruncated = false;

        while (CombinedBytes(
                   CreateText(graph, format, edges, hints, budgetTruncated, maxResponseBytes),
                   CreatePayload(new CallGraphPayloadRequest(
                   graph, edges, hints, direction, requestedDepth, effectiveDepth, topN,
                       scopeType, includeGenerated, budgetTruncated, originalNodes, originalEdges,
                       request.AssemblyNavigation))) > maxResponseBytes)
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

        var payload = CreatePayload(new CallGraphPayloadRequest(
            graph, edges, hints, direction, requestedDepth, effectiveDepth, topN,
            scopeType, includeGenerated, budgetTruncated, originalNodes, originalEdges,
            request.AssemblyNavigation));
        var text = CreateText(graph, format, edges, hints, budgetTruncated, maxResponseBytes);
        if (CombinedBytes(text, payload) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Call-Graph-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen oder symbolIdentifier/scopeType verfeinern.",
                "$.maxResponseBytes");
        }

        return McpToolResults.Text(text, payload);
    }

    private static CallTreePayload CreatePayload(CallGraphPayloadRequest request)
    {
        var graph = request.Graph;
        var edges = request.Edges;
        var hints = request.Hints;
        var direction = request.Direction;
        var requestedDepth = request.RequestedDepth;
        var effectiveDepth = request.EffectiveDepth;
        var topN = request.TopN;
        var scopeType = request.ScopeType;
        var includeGenerated = request.IncludeGenerated;
        var budgetTruncated = request.BudgetTruncated;
        var originalNodes = request.OriginalNodes;
        var originalEdges = request.OriginalEdges;
        var assemblyNavigation = request.AssemblyNavigation;
        var nodes = NodesForEdges(graph, edges);
        var reasons = new List<string>();
        if (graph.TopNTruncated) reasons.Add("topN");
        if (graph.HardCapTruncated) reasons.Add("nodeLimit");
        if (budgetTruncated) reasons.Add("maxResponseBytes");
        return new CallTreePayload(
            graph with { Nodes = nodes, Edges = edges, MethodHints = hints },
            direction,
            requestedDepth,
            effectiveDepth,
            requestedDepth != effectiveDepth,
            topN,
            graph.TopNTruncated || graph.HardCapTruncated || budgetTruncated,
            graph.TopNTruncated,
            originalNodes,
            originalEdges,
            nodes.Count,
            edges.Count,
            reasons,
            new McpScopeMetadata(FindSymbolTool.ToWireValue(scopeType), includeGenerated),
            assemblyNavigation);
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

    private static string CreateFinalText(FinalTextRequest request)
    {
        var projected = request.Graph with
        {
            Nodes = NodesForEdges(request.Graph, request.Edges),
            Edges = request.Edges,
            MethodHints = request.Hints,
        };
        var graphText = GetCallTreeTool.RenderGraph(projected, request.Format);
        if (request.Parts is not null
            && request.BudgetTruncated
            && !string.Concat(request.Parts.Prefix, request.Parts.Suffix, request.NavigationText)
                .Contains("maxResponseBytes", System.StringComparison.Ordinal))
        {
            graphText += $"\n\n[Antwort wegen maxResponseBytes={request.MaxResponseBytes} begrenzt — ganze Graph-Kanten wurden berücksichtigt]";
        }

        if (request.Parts is not null)
        {
            return request.Parts.Prefix + graphText + request.Parts.Suffix;
        }

        return graphText + request.NavigationText;
    }

    private static GraphTextParts? SplitGraphText(
        string text,
        CallGraphPayload graph,
        string format)
    {
        var renderedGraph = GetCallTreeTool.RenderGraph(graph, format);
        var graphStart = text.IndexOf(renderedGraph, System.StringComparison.Ordinal);
        if (graphStart < 0)
        {
            var marker = string.Equals(format, "mermaid", System.StringComparison.Ordinal)
                ? "flowchart TD"
                : FormatRoot(graph);
            graphStart = text.IndexOf(marker, System.StringComparison.Ordinal);
            if (graphStart < 0) return null;
        }

        var graphEnd = FindGraphSuffixStart(text, graphStart + renderedGraph.Length);
        return new GraphTextParts(text[..graphStart], text[graphEnd..]);
    }

    private static int FindGraphSuffixStart(string text, int start)
    {
        var suffixStart = text.Length;
        foreach (var marker in new[] { "\n\n[", "\nStatus: operation=" })
        {
            var index = text.IndexOf(marker, start, System.StringComparison.Ordinal);
            if (index >= 0 && index < suffixStart) suffixStart = index;
        }

        return suffixStart;
    }

    private static string FormatRoot(CallGraphPayload graph)
    {
        var root = graph.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, graph.RootNodeId, System.StringComparison.Ordinal));
        return root is null || string.IsNullOrWhiteSpace(root.DisplayLine)
            ? root?.Name ?? string.Empty
            : $"{root.Name} — {root.DisplayLine}";
    }

    private sealed record GraphTextParts(string Prefix, string Suffix);

    private sealed record FinalTextRequest(
        GraphTextParts? Parts,
        CallGraphPayload Graph,
        string? Format,
        IReadOnlyList<CallGraphEdge> Edges,
        IReadOnlyList<CallGraphMethodHint> Hints,
        bool BudgetTruncated,
        int MaxResponseBytes,
        string NavigationText);

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

    private static int CombinedBytes(string text, CallTreePayload payload) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    private static int CombinedBytes(string text, JsonNode structured) =>
        Encoding.UTF8.GetByteCount(text)
        + Encoding.UTF8.GetByteCount(structured.ToJsonString(McpJsonOptions.Default));

    private static int CombinedBytes(string text, JsonElement structured) =>
        Encoding.UTF8.GetByteCount(text)
        + Encoding.UTF8.GetByteCount(structured.GetRawText());

    private static JsonObject CreatePayloadNode(
        CallTreePayload payload,
        IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints,
        bool budgetTruncated,
        JsonNode? navigation,
        JsonObject? preservedNode = null)
    {
        var scope = payload.Scope is null
            ? McpScopeType.All
            : payload.Scope.RequestedType switch
            {
                "production" => McpScopeType.Production,
                "tests" => McpScopeType.Tests,
                _ => McpScopeType.All,
            };
        var rebuilt = CreatePayload(new CallGraphPayloadRequest(
            payload.Graph,
            edges,
            hints,
            payload.Direction,
            payload.RequestedDepth,
            payload.EffectiveDepth,
            payload.TopN,
            scope,
            payload.Scope?.IncludeGenerated ?? false,
            budgetTruncated,
            payload.TotalNodeCount,
            payload.TotalEdgeCount,
            payload.AssemblyNavigation));
        var rebuiltNode = JsonSerializer.SerializeToNode(rebuilt, McpJsonOptions.Default)!.AsObject();
        var node = preservedNode?.DeepClone().AsObject() ?? new JsonObject();
        foreach (var property in rebuiltNode)
        {
            node[property.Key] = property.Value?.DeepClone();
        }
        if (navigation is not null) node["navigation"] = navigation.DeepClone();
        return node;
    }

    private static string ExtractNavigationText(string text)
    {
        var marker = text.LastIndexOf("\nStatus: operation=", System.StringComparison.Ordinal);
        return marker < 0 ? string.Empty : text[marker..];
    }
}
