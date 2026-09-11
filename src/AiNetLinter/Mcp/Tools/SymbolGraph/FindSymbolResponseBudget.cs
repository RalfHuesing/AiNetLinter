#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class FindSymbolResponseBudget
{
    private const string BudgetReason = "maxResponseBytes";

    internal static CallToolResult Apply(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0
            || result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock)
        {
            return result;
        }

        var source = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (source?["results"] is not JsonArray)
        {
            return result;
        }

        if (CombinedBytes(source, textBlock.Text).Bytes <= maxResponseBytes)
        {
            return result;
        }

        var candidate = (JsonObject)source.DeepClone();
        var combined = CombinedBytes(candidate, textBlock.Text);
        while (combined.Bytes > maxResponseBytes && RemoveLastMatch(candidate))
        {
            combined = CombinedBytes(candidate, textBlock.Text);
        }

        if (combined.Bytes > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen FindSymbol-/Navigations-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Symbol-Entries gekürzt.",
                "$.maxResponseBytes");
        }

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = combined.Text }],
            StructuredContent = JsonSerializer.SerializeToElement(candidate, McpJsonOptions.Default),
        };

        static (int Bytes, string Text) CombinedBytes(JsonObject payload, string originalText) =>
            (Encoding.UTF8.GetByteCount(RenderText(payload, originalText))
                + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length,
            RenderText(payload, originalText));
    }

    private static bool RemoveLastMatch(JsonObject payload)
    {
        if (payload["results"] is not JsonArray results) return false;

        for (var resultIndex = results.Count - 1; resultIndex >= 0; resultIndex--)
        {
            if (results[resultIndex] is not JsonObject pattern
                || pattern["matches"] is not JsonArray matches
                || matches.Count == 0)
            {
                continue;
            }

            matches.RemoveAt(matches.Count - 1);
            pattern["returnedCount"] = matches.Count;
            pattern["isTruncated"] = true;
            AddReason(pattern, BudgetReason);
            payload["returnedCount"] = results
                .OfType<JsonObject>()
                .Sum(item => ReadInt(item, "returnedCount"));
            payload["isTruncated"] = true;
            AddReason(payload, BudgetReason);
            SetNavigationTruncated(payload);
            return true;
        }

        return false;
    }

    private static string RenderText(JsonObject payload, string originalText)
    {
        var projection = (JsonObject)payload.DeepClone();
        projection.Remove("navigation");
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            projection.ToJsonString(McpJsonOptions.Default),
            McpJsonOptions.Default);
        if (batch is null) return originalText;

        var lines = new List<string>();
        for (var index = 0; index < batch.Results.Count; index++)
        {
            if (index > 0) lines.Add("---");
            var pattern = batch.Results[index];
            lines.Add($"### Symbol-Suche: `{pattern.NamePattern}`");
            if (pattern.Matches.Count == 0)
            {
                lines.Add($"Keine Treffer fuer '{pattern.NamePattern}'");
                continue;
            }

            lines.AddRange(pattern.Matches.Select(FindSymbolTool.FormatEntry));
            if (pattern.IsTruncated)
            {
                lines.Add($"Treffer gesamt, {pattern.ReturnedCount} gezeigt (gekürzt durch {BudgetReason}).");
            }
        }

        if (payload["navigation"] is JsonObject)
        {
            lines.Add(string.Empty);
            lines.Add("## Navigation");
            lines.Add("- status: operation=`ok`, completeness=`truncated`");
            lines.Add("- next: `request_detail` — maxResponseBytes erhöhen oder das Suchmuster verfeinern.");
        }

        return string.Join("\n", lines);
    }

    private static void SetNavigationTruncated(JsonObject payload)
    {
        if (payload["navigation"] is not JsonObject navigation) return;
        if (navigation["status"] is JsonObject status)
        {
            status["completeness"] = "truncated";
        }

        navigation["next"] = new JsonObject
        {
            ["kind"] = "request_detail",
            ["action"] = "maxResponseBytes erhöhen oder das Suchmuster verfeinern.",
        };
    }

    private static void AddReason(JsonObject payload, string reason)
    {
        if (payload["truncatedBy"] is not JsonArray reasons)
        {
            reasons = [];
            payload["truncatedBy"] = reasons;
        }

        if (!reasons.Any(item => string.Equals(item?.GetValue<string>(), reason, StringComparison.Ordinal)))
        {
            reasons.Add(reason);
        }
    }

    private static int ReadInt(JsonObject payload, string propertyName) =>
        payload[propertyName] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
}
