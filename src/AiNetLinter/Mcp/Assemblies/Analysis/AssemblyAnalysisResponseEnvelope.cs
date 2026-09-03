#nullable enable

using System;
using System.Collections.Generic;
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
            RecalculateKnownCollectionEnvelope(obj, "files", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "directories", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "callSites", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "results", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "members", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "references", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "referenceSessions", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "diagnostics", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "samples", cursorOffset);
            RecalculateKnownCollectionEnvelope(obj, "namespaces", cursorOffset);
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
        SetIntIfPresent(obj, "totalCount", total);
        UpdateContinuation(obj, returned, total, offset);

        if (returned < total) AddReason(obj, "responseBudget");
    }

    private static void RecalculateKnownCollectionEnvelope(JsonObject obj, string collectionName, int fallbackCursorOffset)
    {
        if (collectionName is "types" or "extensions") return;
        if (obj[collectionName] is not JsonArray collection) return;

        var total = GetKnownTotal(obj, collectionName, collection.Count);
        if (total is null) return;

        var returnedBeforeTrim = GetReturnedBeforeTrim(obj, collectionName);
        var returned = collection.Count;
        var offset = GetContinuationOffset(obj, returnedBeforeTrim, fallbackCursorOffset);
        var truncated = IsTruncated(obj, returned, total.Value) || ContainsTruncatedChild(collection);

        if (collectionName == "directories")
        {
            obj["totalDirectoryCount"] = total.Value;
            obj["returnedDirectoryCount"] = returned;
            obj["directoriesTruncated"] = truncated;
        }
        else if (collectionName is not ("references" or "referenceSessions" or "diagnostics" or "samples" or "namespaces"))
        {
            obj["totalCount"] = total.Value;
            obj["returnedCount"] = returned;
            obj["isTruncated"] = truncated;
            obj["truncated"] = truncated;
        }

        if (truncated && collectionName != "directories")
        {
            AddReason(obj, "responseBudget");
            obj["continuationToken"] = AssemblyPaging.CreateToken(Math.Max(0, offset) + returned);
        }
        else if (collectionName != "directories" && obj["continuationToken"] is not null)
        {
            obj["continuationToken"] = null;
        }

        UpdateNestedCollectionEnvelope(obj, collectionName, total.Value, returned, truncated, offset);
    }

    private static int? GetKnownTotal(JsonObject obj, string collectionName, int fallback)
    {
        return KnownTotalResolvers.TryGetValue(collectionName, out var resolver)
            ? resolver(obj) ?? fallback
            : fallback;
    }

    private static readonly IReadOnlyDictionary<string, Func<JsonObject, int?>> KnownTotalResolvers =
        new Dictionary<string, Func<JsonObject, int?>>(StringComparer.Ordinal)
        {
            ["files"] = obj => GetNestedInt(obj, "summary", "matchedFileCount") ?? GetInt(obj, "totalCount"),
            ["directories"] = obj => GetNestedInt(obj, "summary", "matchedDirectoryCount") ?? GetInt(obj, "totalCount"),
            ["callSites"] = obj => GetNestedInt(obj, "completeness", "totalCallSiteCount") ?? GetInt(obj, "totalCount"),
            ["results"] = obj => GetInt(obj, "requestedCount") ?? GetInt(obj, "totalCount"),
            ["members"] = obj => GetInt(obj, "totalMembers") ?? GetInt(obj, "totalCount"),
            ["references"] = obj => GetNestedInt(obj, "referenceSummary", "totalReferenceCount") ?? GetInt(obj, "totalCount"),
            ["referenceSessions"] = obj => GetNestedInt(obj, "referenceSummary", "totalReferenceSessionCount") ?? GetInt(obj, "totalCount"),
            ["diagnostics"] = ResolveDiagnosticsTotal,
            ["samples"] = ResolveDiagnosticsTotal,
            ["namespaces"] = obj => GetInt(obj, "totalNamespaces") ?? GetInt(obj, "totalCount"),
        };

    private static int? ResolveDiagnosticsTotal(JsonObject obj) =>
        GetInt(obj, "diagnosticTotalCount")
            ?? GetNestedInt(obj, "diagnosticsSummary", "totalCount")
            ?? GetInt(obj, "totalCount");

    private static void UpdateNestedCollectionEnvelope(
        JsonObject obj,
        string collectionName,
        int total,
        int returned,
        bool truncated,
        int offset)
    {
        switch (collectionName)
        {
            case "files":
                UpdateFileTreeEnvelope(obj, total, returned, truncated, offset);
                break;
            case "callSites":
                UpdateCallSiteEnvelope(obj, total, returned, truncated, offset);
                break;
            case "members":
                UpdateMemberEnvelope(obj, truncated);
                break;
            case "references":
                UpdateReferenceEnvelope(obj, new ReferenceEnvelopeUpdate(total, returned, truncated, offset, "shownReferenceCount", "referencesTruncated"));
                break;
            case "referenceSessions":
                UpdateReferenceEnvelope(obj, new ReferenceEnvelopeUpdate(total, returned, truncated, offset, "shownReferenceSessionCount", "referenceSessionsTruncated"));
                break;
            case "diagnostics":
            case "samples":
                UpdateDiagnosticsEnvelope(obj, total, returned, truncated, offset);
                break;
            case "results" when truncated:
                obj["status"] = "truncated";
                obj["detailHint"] = "Body-Ergebnisse wurden wegen des Antwortbudgets gekürzt; maxResponseBytes erhöhen oder den Body gezielt erneut anfordern.";
                break;
        }
    }

    private static void UpdateFileTreeEnvelope(JsonObject obj, int total, int returned, bool truncated, int offset)
    {
        if (obj["completeness"] is not JsonObject completeness) return;
        completeness["shownFileCount"] = returned;
        completeness["truncated"] = truncated || GetBool(completeness, "truncated") == true;
        if (truncated) AddReason(completeness, "responseBudget");
        SetContinuation(completeness, returned, total, offset);
    }

    private static void UpdateCallSiteEnvelope(JsonObject obj, int total, int returned, bool truncated, int offset)
    {
        if (obj["completeness"] is not JsonObject completeness) return;
        completeness["shownCallSiteCount"] = returned;
        var nestedTruncated = truncated
            || GetBool(completeness, "truncatedByMaxResults") == true
            || GetBool(completeness, "truncatedByNodeLimit") == true;
        completeness["truncated"] = nestedTruncated;
        if (nestedTruncated) AddReason(completeness, "responseBudget");
        SetContinuation(completeness, returned, total, offset);
    }

    private static void UpdateMemberEnvelope(JsonObject obj, bool truncated)
    {
        obj["membersTruncated"] = truncated || GetBool(obj, "membersTruncated") == true;
        if (truncated) AddReason(obj, "responseBudget");
    }

    private static void UpdateReferenceEnvelope(
        JsonObject obj,
        ReferenceEnvelopeUpdate update)
    {
        if (obj["referenceSummary"] is not JsonObject summary) return;
        summary[update.ShownName] = update.Returned;
        summary[update.TruncatedName] = update.Truncated;
        if (update.Truncated) AddReason(summary, "responseBudget");
        SetContinuation(summary, update.Returned, update.Total, update.Offset);
    }

    private sealed record ReferenceEnvelopeUpdate(
        int Total,
        int Returned,
        bool Truncated,
        int Offset,
        string ShownName,
        string TruncatedName);

    private static void UpdateDiagnosticsEnvelope(JsonObject obj, int total, int returned, bool truncated, int offset)
    {
        if (obj["diagnosticsSummary"] is not JsonObject summary) return;
        summary["shownCount"] = returned;
        summary["truncated"] = truncated;
        if (truncated) AddReason(summary, "responseBudget");
        SetContinuation(summary, returned, total, offset);
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

    private static bool ContainsTruncatedChild(JsonArray collection) =>
        collection.Any(item => item is JsonObject child
            && (GetBool(child, "isTruncated") == true || GetBool(child, "truncated") == true));

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
        CopyIfPresent(obj, analysis, "totalCount");
        CopyIfPresent(obj, analysis, "returnedCount");

        var outerTruncated = GetBool(obj, "isTruncated") == true || GetBool(obj, "truncated") == true;
        var innerTruncated = GetBool(analysis, "isTruncated") == true || GetBool(analysis, "truncated") == true;
        var mergedTruncated = outerTruncated || innerTruncated;
        obj["isTruncated"] = mergedTruncated;
        obj["truncated"] = mergedTruncated;

        var reasons = MergeReasons(obj["truncatedBy"], analysis["truncatedBy"]);
        if (mergedTruncated && !reasons.Any()) reasons.Add("responseBudget");
        obj["truncatedBy"] = new JsonArray(reasons.Select(reason => JsonValue.Create(reason)).ToArray());

        var outerToken = GetString(obj, "continuationToken");
        var innerToken = GetString(analysis, "continuationToken");
        if (outerToken is not null || innerToken is not null)
        {
            obj["continuationToken"] = outerToken ?? innerToken;
        }
    }

    private static void CopyIfPresent(JsonObject target, JsonObject source, string name)
    {
        if (source[name] is not null) target[name] = source[name]!.DeepClone();
    }

    private static List<string> MergeReasons(JsonNode? outer, JsonNode? inner)
    {
        var reasons = new List<string>();
        foreach (var source in new[] { outer, inner })
        {
            if (source is not JsonArray values) continue;
            foreach (var value in values)
            {
                if (value is JsonValue reason
                    && reason.TryGetValue<string>(out var text)
                    && !reasons.Contains(text, StringComparer.Ordinal))
                {
                    reasons.Add(text);
                }
            }
        }

        return reasons;
    }

    private static void SetContinuation(JsonObject obj, int returned, int total, int offset)
    {
        if (returned < total)
        {
            obj["continuationToken"] = AssemblyPaging.CreateToken(Math.Max(0, offset) + returned);
        }
        else if (obj["continuationToken"] is not null)
        {
            obj["continuationToken"] = null;
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

    private static int? GetNestedInt(JsonObject obj, string objectName, string propertyName) =>
        obj[objectName] is JsonObject nested ? GetInt(nested, propertyName) : null;

    private static string? GetString(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

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
