#nullable enable
// ainetlinter-disable MaxLineCount — die Envelope-Rekalkulation hält die wire-budget-relevanten Projektionen zusammen.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
namespace AiNetLinter.Mcp.Assemblies.Analysis.Responses;
internal static partial class AssemblyAnalysisResponseEnvelope
{
    internal static void RecalculateEnvelopes(JsonNode node, int cursorOffset)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (!IsBudgetMetadata(property.Key)
                    && !IsEnvelopeMetadata(property.Key)
                    && property.Value is not null)
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
            RecalculateGenericCollectionEnvelopes(obj, cursorOffset);
            SyncClassStructureProjection(obj);
            SyncCompositeEnvelope(obj);
            SyncNavigationProjection(obj);
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

    internal static void MarkArrayTruncated(
        JsonObject owner,
        string collectionName,
        int total,
        int returned)
    {
        var envelopeName = $"{collectionName}Envelope";
        var envelope = owner[envelopeName] as JsonObject ?? new JsonObject();
        var effectiveTotal = Math.Max(total, GetInt(envelope, "totalCount") ?? 0);
        envelope["totalCount"] = effectiveTotal;
        envelope["returnedCount"] = returned;
        envelope["isTruncated"] = true;
        envelope["truncatedBy"] = new JsonArray("responseBudget");
        envelope["continuationToken"] = returned < effectiveTotal
            ? AssemblyPaging.CreateToken(returned)
            : null;
        envelope["detailHint"] = $"Array '{collectionName}' wurde wegen des Antwortbudgets gekürzt; maxResponseBytes erhöhen oder die Detailabfrage gezielt erneut anfordern.";
        owner[envelopeName] = envelope;
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
        var offset = GetContinuationOffset(obj, collectionName, returnedBeforeTrim, fallbackCursorOffset);
        var truncated = IsTruncated(obj, collectionName, returned, total);

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
        var offset = GetContinuationOffset(obj, collectionName, returnedBeforeTrim, fallbackCursorOffset);
        var truncated = IsTruncated(obj, collectionName, returned, total.Value) || ContainsTruncatedChild(collection);

        if (collectionName == "directories")
        {
            UpdateDirectoryProjection(obj, total.Value, returned, truncated, offset);
        }
        else if (collectionName is not ("references" or "referenceSessions" or "diagnostics" or "samples" or "namespaces"))
        {
            obj["totalCount"] = total.Value;
            obj["returnedCount"] = returned;
            obj["isTruncated"] = truncated;
            obj["truncated"] = truncated;
            AssemblyAnalysisSearchEnvelope.UpdateKnownContinuation(
                obj, (collectionName, truncated, returned, total.Value, offset));
        }

        UpdateNestedCollectionEnvelope(obj, collectionName, total.Value, returned, truncated, offset);
    }

    private static void UpdateDirectoryProjection(JsonObject obj, int total, int returned, bool truncated, int offset)
    {
        obj["totalDirectoryCount"] = total;
        obj["returnedDirectoryCount"] = returned;
        obj["directoriesTruncated"] = truncated;
        obj["directoriesTruncatedBy"] = CreateReasons(
            truncated,
            obj["directoriesTruncatedBy"],
            (obj["completeness"] as JsonObject)?["directoryTruncatedBy"]);
        obj["directoriesContinuationToken"] = truncated
            ? AssemblyPaging.CreateToken(Math.Max(0, offset) + returned)
            : null;
        obj["directoriesDetailHint"] = truncated
            ? "Verzeichnisse wurden wegen des Antwortbudgets gekürzt; maxResponseBytes erhöhen oder die Verzeichnisabfrage gezielt erneut anfordern."
            : null;
    }

}
