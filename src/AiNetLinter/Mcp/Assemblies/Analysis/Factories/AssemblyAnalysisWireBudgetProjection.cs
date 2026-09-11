#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal static class AssemblyAnalysisWireBudgetProjection
{
    internal static CallToolResult Apply(CallToolResult result, int budget, int cursorOffset)
    {
        if (budget < AssemblyAnalysisResponseLimits.MinimumResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                $"Das angeforderte Assembly-Antwortbudget muss mindestens {AssemblyAnalysisResponseLimits.MinimumResponseBytes} Bytes betragen.",
                "maxResponseBytes erhöhen; unterhalb dieser Grenze wird kein unvollständiger Wire-Envelope erzeugt.");
        }

        var withBudget = AddWireBudgetMetadata(
            result,
            budget,
            IsStructuredTruncated(result.StructuredContent));
        withBudget = ReserveTextBudget(withBudget, budget);
        if (McpResponseSize.From(withBudget).TotalBytes <= budget) return withBudget;

        withBudget = TrimStructuredToBudget(withBudget, budget, cursorOffset);
        withBudget = SynchronizeClassStructureText(withBudget);

        if (McpResponseSize.From(withBudget).TotalBytes > budget)
        {
            withBudget = MinimizeEnvelope(withBudget, budget);
        }

        withBudget = AddWireBudgetMetadata(withBudget, budget, IsStructuredTruncated(withBudget.StructuredContent));
        if (McpResponseSize.From(withBudget).TotalBytes <= budget) return withBudget;

        return McpToolResults.InvalidArgument(
            $"Das Assembly-Antwortbudget von {budget} Bytes ist für den minimalen Wire-Envelope nicht repräsentierbar.",
            $"maxResponseBytes auf mindestens {AssemblyAnalysisResponseLimits.MinimumResponseBytes} Bytes erhöhen.");
    }

    private static CallToolResult ReserveTextBudget(CallToolResult result, int budget)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object }) return result;

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        var textBudget = Math.Max(1, budget / 4);
        if (Encoding.UTF8.GetByteCount(text) <= textBudget) return result;

        // A text-only trim is already a wire-level truncation. Preserve that
        // fact in the structured envelope as well, so consumers do not mistake
        // the navigation metadata for a complete response.
        return AddWireBudgetMetadata(
            McpToolResults.ReplaceText(result, AssemblyAnalysisResponse.TrimTextPreservingNavigation(text, textBudget)),
            budget,
            isTruncated: true);
    }

    private static CallToolResult TrimStructuredToBudget(
        CallToolResult result,
        int budget,
        int cursorOffset)
    {
        var current = result;
        for (var attempt = 0; attempt < 128 && McpResponseSize.From(current).TotalBytes > budget; attempt++)
        {
            if (current.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
            {
                return TrimTextToBudget(current, budget - McpResponseSize.From(current).StructuredBytes);
            }

            var available = Math.Max(1, budget - McpResponseSize.From(current).TextBytes);
            var trimmed = TrimStructured(structured, available, cursorOffset);
            if (trimmed.GetRawText() == structured.GetRawText())
            {
                return TrimTextToBudget(current, budget - McpResponseSize.From(current).StructuredBytes);
            }

            current = AddWireBudgetMetadata(
                ReplaceStructured(current, trimmed),
                budget,
                isTruncated: true);
        }

        return current;
    }

    private static CallToolResult TrimTextToBudget(CallToolResult result, int budget)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        return McpToolResults.ReplaceText(result, AssemblyAnalysisResponse.TrimTextPreservingNavigation(text, Math.Max(1, budget)));
    }

    private static CallToolResult SynchronizeClassStructureText(CallToolResult result)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock)
        {
            return result;
        }

        var root = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        var classStructure = root?["classStructure"] as JsonObject;
        if (classStructure?["members"] is not JsonArray members
            || classStructure["totalMemberCount"] is not JsonValue totalValue
            || !totalValue.TryGetValue<int>(out var totalMemberCount))
        {
            return result;
        }

        const string marker = "- Member Count: ";
        var markerIndex = textBlock.Text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return result;

        var valueStart = markerIndex + marker.Length;
        var separatorIndex = textBlock.Text.IndexOf(" von ", valueStart, StringComparison.Ordinal);
        var lineEnd = textBlock.Text.IndexOf('\n', valueStart);
        if (separatorIndex < valueStart || (lineEnd >= 0 && separatorIndex > lineEnd)) return result;

        var shownMemberCount = members.Count;
        var updatedText = textBlock.Text[..valueStart]
            + shownMemberCount
            + textBlock.Text[separatorIndex..];
        return McpToolResults.ReplaceText(result, updatedText);
    }

    private static CallToolResult MinimizeEnvelope(CallToolResult result, int budget)
    {
        var minimalPayload = new JsonObject
        {
            ["isTruncated"] = true,
            ["truncated"] = true,
            ["wireTruncated"] = true,
            ["truncatedBy"] = new JsonArray("responseBudget"),
            ["detailHint"] = "Die strukturierte Nutzlast wurde auf den minimalen Antwortumfang gekürzt; maxResponseBytes erhöhen oder die Detailabfrage gezielt erneut anfordern.",
        };
        if (result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
            && JsonNode.Parse(structured.GetRawText()) is JsonObject original)
        {
            if (original["navigation"] is JsonNode navigation)
            {
                minimalPayload["navigation"] = navigation.DeepClone();
                MarkNavigationTruncated(minimalPayload);
            }

            if (original["analysis"] is JsonNode analysis)
            {
                minimalPayload["analysis"] = analysis.DeepClone();
            }
        }

        var minimal = ReplaceStructured(
            result,
            JsonSerializer.SerializeToElement(minimalPayload, McpJsonOptions.Default));
        return AddWireBudgetMetadata(
            TrimTextToBudget(minimal, budget - McpResponseSize.From(minimal).StructuredBytes),
            budget,
            isTruncated: true);
    }

    private static CallToolResult AddWireBudgetMetadata(
        CallToolResult result,
        int budget,
        bool isTruncated)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var node = JsonNode.Parse(structured.GetRawText()) as JsonObject ?? new JsonObject();
        var candidate = result;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var measurement = McpResponseSize.From(candidate);
            var existingWireTruncation = node["wireTruncated"] is JsonValue existing
                && existing.TryGetValue<bool>(out var existingValue)
                && existingValue;
            var wireTruncated = isTruncated || existingWireTruncation;
            if (wireTruncated)
            {
                AssemblyAnalysisResponseEnvelope.AddReason(node, "responseBudget");
                MarkNavigationTruncated(node);
            }
            node["wireBudget"] = new JsonObject
            {
                ["limitBytes"] = budget,
                ["textBytes"] = measurement.TextBytes,
                ["structuredBytes"] = measurement.StructuredBytes,
                ["totalBytes"] = measurement.TotalBytes,
                ["truncated"] = wireTruncated,
            };
            node["wireTruncated"] = wireTruncated;
            var next = ReplaceStructured(candidate, JsonSerializer.SerializeToElement(node, McpJsonOptions.Default));
            if (McpResponseSize.From(next) == measurement
                && next.StructuredContent?.GetRawText() == candidate.StructuredContent?.GetRawText()) return next;
            candidate = next;
        }

        return candidate;
    }

    private static JsonElement TrimStructured(JsonElement structured, int budget, int cursorOffset)
    {
        var node = JsonNode.Parse(structured.GetRawText()) ?? new JsonObject();
        var continuationBinding = AssemblyPaging.FindBinding(node);
        MarkStructuredTruncated(node);
        while (JsonSerializer.SerializeToUtf8Bytes(node, McpJsonOptions.Default).Length > budget
            && TryTrimNode(node))
        {
            // Projection is intentionally batched; envelope fields are rebuilt once below.
        }

        AssemblyAnalysisResponseEnvelope.RecalculateEnvelopes(node, cursorOffset);
        AssemblyPaging.RebindContinuationTokens(node, continuationBinding);

        return JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
    }

    private static bool IsStructuredTruncated(JsonElement? structured) =>
        structured is { ValueKind: JsonValueKind.Object } value
        && value.TryGetProperty("wireTruncated", out var wireTruncated)
        && wireTruncated.ValueKind == JsonValueKind.True;

    private static bool TryTrimNode(JsonNode node, string? propertyName = null) =>
        node switch
        {
            JsonObject obj => TryTrimObject(obj),
            JsonArray array => IsTrimCandidate(propertyName) && TryTrimArray(array),
            _ => false,
        };

    private static bool TryTrimObject(JsonObject obj) =>
        TryTrimCollections(obj)
        || TryTrimObjectChildren(obj)
        || TryTrimOptionalSections(obj)
        || TryTrimObjectStrings(obj)
        || TryRemoveLargestObjectProperty(obj);

    private static bool TryTrimCollections(JsonObject obj)
    {
        foreach (var collectionName in ResultCollections)
        {
            if (obj[collectionName] is not JsonArray collection || collection.Count <= 1) continue;
            var removeCount = collection.Count > 16 ? Math.Max(1, collection.Count / 4) : 1;
            for (var index = 0; index < removeCount; index++)
            {
                collection.RemoveAt(collection.Count - 1);
            }
            return true;
        }

        return false;
    }

    private static bool TryTrimOptionalSections(JsonObject obj)
    {
        foreach (var section in new[] { "assemblyAnalysis", "body", "classStructure", "metrics", "impact", "callers" })
        {
            if (obj[section] is not { } original
                || original is JsonObject sectionObject && sectionObject["status"] is not null
                || section == "body"
                    && original is JsonObject bodyObject
                    && ResultCollections.Any(collection => bodyObject[collection] is JsonArray)) continue;

            obj[section] = new JsonObject
            {
                ["status"] = "truncated",
                ["truncated"] = true,
                ["truncatedBy"] = new JsonArray("responseBudget"),
                ["detailHint"] = $"Abschnitt '{section}' wurde wegen des Antwortbudgets gekürzt; maxResponseBytes oder detailLevel erhöhen und den Abschnitt gezielt erneut anfordern.",
                ["continuationToken"] = AssemblyAnalysisResponseEnvelope.ExtractContinuationToken(original),
            };
            return true;
        }

        return false;
    }

    private static bool TryTrimObjectStrings(JsonObject obj)
    {
        foreach (var property in obj.ToList())
        {
            if (IsBudgetMetadata(property.Key) || IsEnvelopeMetadata(property.Key)) continue;
            if (property.Value is JsonValue value
                && value.TryGetValue<string>(out var text)
                && text.Length > 256)
            {
                obj[property.Key] = McpUtf8BudgetTrimmer.TrimWithoutEllipsis(text, 256);
                if (property.Key == "body")
                {
                    obj["isTruncated"] = true;
                    obj["truncated"] = true;
                    AssemblyAnalysisResponseEnvelope.AddReason(obj, "responseBudget");
                    obj["detailHint"] = "Body wegen des Antwortbudgets gekürzt; maxResponseBytes erhöhen oder den Body gezielt mit kleinerem Zeilenbereich anfordern.";
                }
                return true;
            }
        }

        return false;
    }

    private static bool TryTrimObjectChildren(JsonObject obj)
    {
        foreach (var property in obj)
        {
            if (IsBudgetMetadata(property.Key)) continue;
            if (AssemblyAnalysisResponseUnknownArrays.TryTrim(obj, property.Key, property.Value)) return true;
            if (property.Value is JsonArray && IsTrimCandidate(property.Key)
                && TryTrimNode(property.Value, property.Key)) return true;
            if (property.Value is JsonObject && IsTrimContainer(property.Key)
                && property.Key is not ("completeness" or "summary" or "referenceSummary" or "diagnosticsSummary")
                && TryTrimNode(property.Value, property.Key)) return true;
        }

        return false;
    }

    private static bool TryRemoveLargestObjectProperty(JsonObject obj)
    {
        var removable = obj
            .Where(property => !IsBudgetMetadata(property.Key)
                && !IsEnvelopeMetadata(property.Key)
                && !IsTrimCandidate(property.Key)
                && !IsTrimContainer(property.Key)
                && (property.Value is not JsonArray array
                    || array.Count == 0
                    || ResultCollections.Contains(property.Key, StringComparer.Ordinal))
                && property.Key is not ("body" or "classStructure" or "metrics" or "impact" or "callers" or "fileTree"))
            .OrderByDescending(property => property.Value is null
                ? 0
                : JsonSerializer.SerializeToUtf8Bytes(property.Value, McpJsonOptions.Default).Length)
            .FirstOrDefault();
        if (removable.Key is null) return false;
        obj.Remove(removable.Key);
        return true;
    }

    private static bool TryTrimArray(JsonArray array)
    {
        foreach (var item in array)
        {
            if (item is not null && TryTrimNode(item)) return true;
        }

        if (array.Count <= 1) return false;
        array.RemoveAt(array.Count - 1);
        return true;
    }

    private static void MarkStructuredTruncated(JsonNode node)
    {
        if (node is not JsonObject obj) return;
        obj["isTruncated"] = true;
        obj["truncated"] = true;
        obj["wireTruncated"] = true;
        AssemblyAnalysisResponseEnvelope.AddReason(obj, "responseBudget");
        MarkNavigationTruncated(obj);
    }

    private static void MarkNavigationTruncated(JsonObject payload)
    {
        if (payload["navigation"] is JsonObject navigation)
        {
            if (navigation["status"] is JsonObject status)
            {
                status["completeness"] = "truncated";
            }
        }
    }

    private static readonly string[] ResultCollections =
        [
            "types", "extensions", "files", "directories", "callSites", "results", "members",
            "references", "referenceSessions", "diagnostics", "samples", "namespaces",
        ];

    private static bool IsTrimCandidate(string? propertyName) =>
        propertyName is not null && ResultCollections.Contains(propertyName, StringComparer.Ordinal);

    private static bool IsTrimContainer(string propertyName) =>
        propertyName is "assemblyAnalysis" or "body" or "classStructure" or "metrics" or "impact"
            or "callers" or "fileTree" or "assemblySearch" or "completeness" or "summary" or "referenceSummary"
            or "diagnosticsSummary";

    private static bool IsBudgetMetadata(string name) =>
        name is "analysis" or "navigation" or "wireBudget" or "wireTruncated" or "truncatedBy";

    private static bool IsEnvelopeMetadata(string name) =>
        name is "totalTypes" or "totalExtensions" or "totalCount" or "returnedCount"
            or "shownCount" or "isTruncated" or "truncated" or "continuationToken"
            or "types" or "extensions" or "id" or "handoff" or "allowedFollowUpTools"
            or "name" or "namespace" or "kind" or "accessibility" or "status" or "detailHint"
            or "contentMode" or "bodyAvailability"
            || name.EndsWith("Envelope", StringComparison.Ordinal);

    private static CallToolResult ReplaceStructured(CallToolResult result, JsonElement structured) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = structured,
        };

}
