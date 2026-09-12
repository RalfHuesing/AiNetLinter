#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Mcp.Tools.Common;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal static partial class CallGraphResponseBudget
{
    private sealed record FinalResponseProjection(
        CallTreePayload Payload,
        JsonObject PreservedNode,
        JsonNode? Navigation,
        GraphTextParts? TextParts,
        string Format,
        string NavigationText,
        List<CallGraphEdge> Edges,
        List<CallGraphMethodHint> Hints,
        bool BudgetTruncated);

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0 || McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        return McpToolResults.InvalidArgument(
            "maxResponseBytes ist für den Call-Graph zu klein.",
            "maxResponseBytes erhöhen oder includeReferences/symbolIdentifier verfeinern.",
            "$.maxResponseBytes");
    }

    private static FinalResponseProjection CreateFinalProjection(string text, CallTreePayload payload, JsonObject node)
    {
        var format = text.Contains("flowchart TD", System.StringComparison.Ordinal) ? "mermaid" : "ascii";
        return new(payload, node, node["navigation"]?.DeepClone(), SplitGraphText(text, payload.Graph, format),
            format, ExtractNavigationText(text), payload.Graph.Edges.ToList(),
            payload.Graph.MethodHints?.ToList() ?? [],
            payload.TruncatedBy?.Contains("maxResponseBytes", System.StringComparer.Ordinal) == true);
    }

    private static CallToolResult TrimFinalProjection(CallToolResult result, FinalResponseProjection projection, int maxResponseBytes)
    {
        var budgetTruncated = projection.BudgetTruncated;
        var edges = projection.Edges;
        var hints = projection.Hints;
        while (FinalProjectionExceedsBudget(projection, edges, hints, budgetTruncated, maxResponseBytes))
        {
            budgetTruncated = true;
            if (hints.Count > 0) { hints.RemoveAt(hints.Count - 1); continue; }
            if (edges.Count == 0) break;
            edges.RemoveAt(edges.Count - 1);
        }

        var text = CreateProjectedText(projection, edges, hints, budgetTruncated, maxResponseBytes);
        var node = CreateFinalPayloadNode(projection, edges, hints, budgetTruncated, text, maxResponseBytes);
        if (CombinedBytes(text, node) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Call-Graph-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen oder symbolIdentifier/scopeType verfeinern.", "$.maxResponseBytes");
        }

        return new CallToolResult { IsError = result.IsError, Content = [new TextContentBlock { Text = text }] };
    }

    private static bool FinalProjectionExceedsBudget(FinalResponseProjection projection, IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints, bool budgetTruncated, int maxResponseBytes)
    {
        var text = CreateProjectedText(projection, edges, hints, budgetTruncated, maxResponseBytes);
        return CombinedBytes(text, CreateFinalPayloadNode(projection, edges, hints, budgetTruncated, text, maxResponseBytes)) > maxResponseBytes;
    }

    private static string CreateProjectedText(FinalResponseProjection projection, IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints, bool budgetTruncated, int maxResponseBytes) =>
        CreateFinalText(new FinalTextRequest(projection.TextParts, projection.Payload.Graph, projection.Format,
            edges, hints, budgetTruncated, maxResponseBytes, projection.NavigationText));

    private static JsonObject CreateFinalPayloadNode(FinalResponseProjection projection, IReadOnlyList<CallGraphEdge> edges,
        IReadOnlyList<CallGraphMethodHint> hints, bool budgetTruncated, string text, int maxResponseBytes) =>
        RefreshWireBudget(CreatePayloadNode(projection.Payload, edges, hints, budgetTruncated,
            projection.Navigation, projection.PreservedNode), text, maxResponseBytes, budgetTruncated);

    private static JsonObject RefreshWireBudget(JsonObject node, string text, int maxResponseBytes, bool budgetTruncated)
    {
        var existingTruncated = node["wireTruncated"] is JsonValue wireTruncated && wireTruncated.TryGetValue<bool>(out var wireValue) && wireValue;
        if (node["wireBudget"] is JsonObject existingBudget && existingBudget["truncated"] is JsonValue budgetValue
            && budgetValue.TryGetValue<bool>(out var isTruncated)) existingTruncated |= isTruncated;

        var textBytes = Encoding.UTF8.GetByteCount(text);
        node["wireBudget"] = new JsonObject { ["limitBytes"] = maxResponseBytes, ["textBytes"] = textBytes,
            ["structuredBytes"] = 0, ["totalBytes"] = 0, ["truncated"] = existingTruncated || budgetTruncated };
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var structuredBytes = Encoding.UTF8.GetByteCount(node.ToJsonString(McpJsonOptions.Default));
            var wireBudget = node["wireBudget"]!.AsObject();
            wireBudget["structuredBytes"] = structuredBytes;
            wireBudget["totalBytes"] = textBytes + structuredBytes;
            var next = JsonNode.Parse(node.ToJsonString(McpJsonOptions.Default))!.AsObject();
            var nextBytes = Encoding.UTF8.GetByteCount(next.ToJsonString(McpJsonOptions.Default));
            if (next["wireBudget"]!.AsObject()["structuredBytes"]?.GetValue<int>() == nextBytes
                && next["wireBudget"]!.AsObject()["totalBytes"]?.GetValue<int>() == textBytes + nextBytes) return next;
            node = next;
        }

        return node;
    }
}
