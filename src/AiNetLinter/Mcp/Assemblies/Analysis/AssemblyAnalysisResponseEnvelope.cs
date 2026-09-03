#nullable enable

using System;
using System.Linq;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyAnalysisResponseEnvelope
{
    internal static void RecalculateEnvelopes(JsonNode node, int cursorOffset)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (!IsBudgetMetadata(property.Key) && property.Value is not null)
                {
                    RecalculateEnvelopes(property.Value, cursorOffset);
                }
            }

            RecalculateCollectionEnvelope(obj, "types", "totalTypes", cursorOffset);
            RecalculateCollectionEnvelope(obj, "extensions", "totalExtensions", cursorOffset);
            SyncCompositeEnvelope(obj);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null) RecalculateEnvelopes(item, cursorOffset);
            }
        }
    }

    internal static string? ExtractContinuationToken(JsonNode? section) =>
        section is JsonObject obj
            && obj["continuationToken"] is JsonValue token
            && token.TryGetValue<string>(out var value)
                ? value
                : null;

    internal static void AddReason(JsonObject obj, string reason)
    {
        if (obj["truncatedBy"] is not JsonArray reasons)
        {
            reasons = new JsonArray();
            obj["truncatedBy"] = reasons;
        }

        if (!reasons.Any(item => string.Equals(item?.GetValue<string>(), reason, StringComparison.Ordinal)))
        {
            reasons.Add(reason);
        }
    }

    private static void RecalculateCollectionEnvelope(
        JsonObject obj,
        string collectionName,
        string totalName,
        int fallbackCursorOffset)
    {
        if (obj[totalName] is not JsonValue totalValue
            || !totalValue.TryGetValue<int>(out var total)) return;

        var returnedBeforeTrim = GetReturnedBeforeTrim(obj, collectionName);
        var returned = GetReturnedCount(obj, collectionName);
        var offset = GetContinuationOffset(obj, returnedBeforeTrim, fallbackCursorOffset);
        var truncated = IsTruncated(obj, returned, total);

        UpdateCounts(obj, returned, truncated);
        UpdateContinuation(obj, returned, total, offset);

        if (returned < total) AddReason(obj, "responseBudget");
    }

    private static int GetReturnedBeforeTrim(JsonObject obj, string collectionName) =>
        GetInt(obj, "returnedCount")
            ?? GetInt(obj, "shownCount")
            ?? GetInt(obj, collectionName)
            ?? 0;

    private static int GetReturnedCount(JsonObject obj, string collectionName) =>
        obj[collectionName] is JsonArray items ? items.Count : 0;

    private static bool IsTruncated(JsonObject obj, int returned, int total) =>
        (GetBool(obj, "truncated") ?? false)
            || (GetBool(obj, "isTruncated") ?? false)
            || returned < total;

    private static void UpdateCounts(JsonObject obj, int returned, bool truncated)
    {
        SetIntIfPresent(obj, "shownCount", returned);
        SetIntIfPresent(obj, "returnedCount", returned);
        SetBoolIfPresent(obj, "truncated", truncated);
        SetBoolIfPresent(obj, "isTruncated", truncated);
    }

    private static void UpdateContinuation(JsonObject obj, int returned, int total, int offset)
    {
        if (obj["continuationToken"] is not null)
        {
            obj["continuationToken"] = returned < total
                ? AssemblyPaging.CreateToken(Math.Max(0, offset) + returned)
                : null;
        }
    }

    private static void SyncCompositeEnvelope(JsonObject obj)
    {
        if (obj["assemblyAnalysis"] is not JsonObject analysis) return;
        foreach (var name in new[] { "totalCount", "returnedCount", "isTruncated", "continuationToken", "truncatedBy" })
        {
            if (analysis[name] is not null) obj[name] = analysis[name]!.DeepClone();
        }
    }

    private static int GetContinuationOffset(JsonObject obj, int returnedBeforeTrim, int fallback)
    {
        if (obj["continuationToken"] is JsonValue token
            && token.TryGetValue<string>(out var value)
            && int.TryParse(value, out var absolute)
            && absolute >= returnedBeforeTrim)
        {
            return absolute - returnedBeforeTrim;
        }

        return fallback;
    }

    private static int? GetInt(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value && value.TryGetValue<int>(out var result) ? result : null;

    private static bool? GetBool(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;

    private static void SetIntIfPresent(JsonObject obj, string propertyName, int value)
    {
        if (obj[propertyName] is not null) obj[propertyName] = value;
    }

    private static void SetBoolIfPresent(JsonObject obj, string propertyName, bool value)
    {
        if (obj[propertyName] is not null) obj[propertyName] = value;
    }

    private static bool IsBudgetMetadata(string name) =>
        name is "analysis" or "wireBudget" or "wireTruncated" or "truncatedBy";
}
