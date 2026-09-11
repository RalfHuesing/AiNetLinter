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
internal static class CallGraphResponseBudget
{
    internal sealed record CallGraphResponseRequest(
        CallGraphPayload Graph,
        string? Format,
        string Direction,
        int RequestedDepth,
        int EffectiveDepth,
        int TopN,
        McpScopeType ScopeType,
        bool IncludeGenerated,
        int MaxResponseBytes,
        AssemblyNavigationSummary? AssemblyNavigation = null);

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

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0
            || result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } content)
        {
            return result;
        }

        var node = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        var payload = JsonSerializer.Deserialize<CallTreePayload>(
            structured.GetRawText(), McpJsonOptions.Default);
        if (node is null || payload is null || node["graph"] is not JsonObject)
        {
            return CombinedBytes(content.Text, structured) <= maxResponseBytes
                ? result
                : McpToolResults.InvalidArgument(
                    "maxResponseBytes ist für den Assembly-Call-Graph zu klein.",
                    "maxResponseBytes erhöhen oder includeReferences/symbolIdentifier verfeinern.",
                    "$.maxResponseBytes");
        }

        var navigation = node["navigation"]?.DeepClone();
        var format = content.Text.Contains("flowchart TD", System.StringComparison.Ordinal)
            ? "mermaid"
            : "ascii";
        var textParts = SplitGraphText(content.Text, payload.Graph, format);
        var navigationText = ExtractNavigationText(content.Text);
        var edges = payload.Graph.Edges.ToList();
        var hints = payload.Graph.MethodHints?.ToList() ?? [];
        var budgetTruncated = payload.TruncatedBy?.Contains("maxResponseBytes", System.StringComparer.Ordinal) == true;

        while (CombinedBytes(
                   CreateFinalText(
                       textParts,
                       payload.Graph,
                       format,
                       edges,
                       hints,
                       budgetTruncated,
                       maxResponseBytes,
                       navigationText),
                   CreateFinalPayloadNode(
                       payload,
                       edges,
                       hints,
                       budgetTruncated,
                       navigation,
                       node,
                       CreateFinalText(
                           textParts,
                           payload.Graph,
                           format,
                           edges,
                           hints,
                           budgetTruncated,
                           maxResponseBytes,
                           navigationText),
                       maxResponseBytes)) > maxResponseBytes)
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

        var finalText = CreateFinalText(
            textParts,
            payload.Graph,
            format,
            edges,
            hints,
            budgetTruncated,
            maxResponseBytes,
            navigationText);
        var finalNode = CreateFinalPayloadNode(
            payload,
            edges,
            hints,
            budgetTruncated,
            navigation,
            node,
            finalText,
            maxResponseBytes);
        if (CombinedBytes(finalText, finalNode) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Call-Graph-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen oder symbolIdentifier/scopeType verfeinern.",
                "$.maxResponseBytes");
        }

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = finalText }],
            StructuredContent = JsonSerializer.SerializeToElement(finalNode, McpJsonOptions.Default),
        };
    }

    private static JsonObject CreateFinalPayloadNode(
        CallTreePayload payload,
        IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints,
        bool budgetTruncated,
        JsonNode? navigation,
        JsonObject? preservedNode,
        string text,
        int maxResponseBytes) =>
        RefreshWireBudget(
            CreatePayloadNode(payload, edges, hints, budgetTruncated, navigation, preservedNode),
            text,
            maxResponseBytes,
            budgetTruncated);

    private static JsonObject RefreshWireBudget(
        JsonObject node,
        string text,
        int maxResponseBytes,
        bool budgetTruncated)
    {
        var existingTruncated = node["wireTruncated"] is JsonValue wireTruncated
            && wireTruncated.TryGetValue<bool>(out var wireValue)
            && wireValue;
        if (node["wireBudget"] is JsonObject existingBudget
            && existingBudget["truncated"] is JsonValue budgetTruncatedValue
            && budgetTruncatedValue.TryGetValue<bool>(out var budgetValue))
        {
            existingTruncated |= budgetValue;
        }

        var textBytes = Encoding.UTF8.GetByteCount(text);
        node["wireBudget"] = new JsonObject
        {
            ["limitBytes"] = maxResponseBytes,
            ["textBytes"] = textBytes,
            ["structuredBytes"] = 0,
            ["totalBytes"] = 0,
            ["truncated"] = existingTruncated || budgetTruncated,
        };

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var structuredBytes = Encoding.UTF8.GetByteCount(
                node.ToJsonString(McpJsonOptions.Default));
            var totalBytes = textBytes + structuredBytes;
            var wireBudget = node["wireBudget"]!.AsObject();
            wireBudget["textBytes"] = textBytes;
            wireBudget["structuredBytes"] = structuredBytes;
            wireBudget["totalBytes"] = totalBytes;

            var next = JsonNode.Parse(node.ToJsonString(McpJsonOptions.Default))!.AsObject();
            var nextStructuredBytes = Encoding.UTF8.GetByteCount(
                next.ToJsonString(McpJsonOptions.Default));
            var nextWireBudget = next["wireBudget"]!.AsObject();
            if (nextWireBudget["structuredBytes"]?.GetValue<int>() == nextStructuredBytes
                && nextWireBudget["totalBytes"]?.GetValue<int>() == textBytes + nextStructuredBytes)
            {
                return next;
            }

            node = next;
        }

        return node;
    }

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

    private static string CreateFinalText(
        GraphTextParts? parts,
        CallGraphPayload graph,
        string? format,
        IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints,
        bool budgetTruncated,
        int maxResponseBytes,
        string navigationText)
    {
        var projected = graph with
        {
            Nodes = NodesForEdges(graph, edges),
            Edges = edges,
            MethodHints = hints,
        };
        var graphText = GetCallTreeTool.RenderGraph(projected, format);
        if (parts is not null
            && budgetTruncated
            && !string.Concat(parts?.Prefix, parts?.Suffix, navigationText)
                .Contains("maxResponseBytes", System.StringComparison.Ordinal))
        {
            graphText += $"\n\n[Antwort wegen maxResponseBytes={maxResponseBytes} begrenzt — ganze Graph-Kanten wurden berücksichtigt]";
        }

        if (parts is not null)
        {
            return parts.Prefix + graphText + parts.Suffix;
        }

        return graphText + navigationText;
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
