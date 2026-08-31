#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisResponseBudgetCompactor
{
    private static readonly string[] ArrayPropertyNames =
    [
        "members", "attributes", "parameters", "genericParameters", "constraints",
        "callSites", "diagnostics",
    ];

    private static readonly string[] OptionalPropertyNames =
    [
        "diagnostics", "callSites", "referenceSessions", "references", "types", "extensions", "results",
    ];

    private static readonly string[] DeepDetailPropertyNames =
    [
        "attributes", "parameters", "genericParameters", "constraints", "applicabilityReason",
        "canonicalPath", "assemblyHash", "generatedPath",
    ];

    internal static void Compact(JsonObject node, int maxBytes)
    {
        CompactDiagnosticSamples(node);
        if (SerializedSize(node) > maxBytes) CompactRepeatedMetadata(node);

        while (SerializedSize(node) > maxBytes)
        {
            if (TrimOneArrayItem(node)
                || RemoveOneOptionalProperty(node)
                || TrimOneLongString(node, includeStablePaths: false)
                || TrimOneLongString(node, includeStablePaths: true))
            {
                continue;
            }

            break;
        }

        SynchronizeDiagnosticSummary(node);
        SynchronizeReferenceSummary(node);
    }

    private static void CompactDiagnosticSamples(JsonObject node)
    {
        if (node["diagnosticsSummary"] is not JsonObject summary) return;
        ClearSamples(summary);
        if (summary["root"] is JsonObject root) ClearSamples(root);
        if (summary["transitive"] is JsonObject transitive) ClearSamples(transitive);
        RemoveEmptyReferenceSummary(node);
    }

    private static void RemoveEmptyReferenceSummary(JsonObject node)
    {
        if (node["referenceSummary"] is not JsonObject referenceSummary
            || referenceSummary["totalReferenceCount"]?.GetValue<int>() != 0
            || referenceSummary["totalReferenceSessionCount"]?.GetValue<int>() != 0)
        {
            return;
        }

        node.Remove("referenceSummary");
    }

    private static void ClearSamples(JsonObject node) =>
        node["samples"] = new JsonArray();

    private static void CompactRepeatedMetadata(JsonObject node)
    {
        RemoveProperty(node, "origin", "canonicalPath");
        RemoveProperty(node, "analysis", "assemblyHash");
    }

    private static void RemoveProperty(JsonObject node, string parentName, string propertyName)
    {
        if (node[parentName] is JsonObject parent) parent.Remove(propertyName);
    }

    private static int SerializedSize(JsonNode node) =>
        JsonSerializer.SerializeToUtf8Bytes(node, McpJsonOptions.Default).Length;

    private static void SynchronizeDiagnosticSummary(JsonObject node)
    {
        if (node["diagnostics"] is JsonArray diagnostics
            && node["diagnosticsSummary"] is JsonObject summary)
        {
            summary["shownCount"] = diagnostics.Count;
        }
    }

    private static void SynchronizeReferenceSummary(JsonObject node)
    {
        if (node["referenceSummary"] is not JsonObject summary) return;
        SynchronizeReferenceArray(node, summary, "references", "totalReferenceCount", "shownReferenceCount", "referencesTruncated");
        SynchronizeReferenceArray(node, summary, "referenceSessions", "totalReferenceSessionCount", "shownReferenceSessionCount", "referenceSessionsTruncated");
    }

    private static void SynchronizeReferenceArray(
        JsonObject node,
        JsonObject summary,
        string arrayName,
        string totalName,
        string shownName,
        string truncatedName)
    {
        if (node[arrayName] is not JsonArray values) return;
        var total = summary[totalName]?.GetValue<int>() ?? values.Count;
        summary[shownName] = values.Count;
        summary[truncatedName] = values.Count < total;
    }

    private static bool TrimOneArrayItem(JsonNode node) => node switch
    {
        JsonObject obj => TrimObjectArrayItem(obj),
        JsonArray values => TrimArrayItem(values),
        _ => false,
    };

    private static bool TrimObjectArrayItem(JsonObject obj)
    {
        foreach (var propertyName in ArrayPropertyNames)
        {
            if (TryTrimNamedArray(obj, propertyName)) return true;
        }

        foreach (var property in obj)
        {
            if (property.Value is not null && TrimOneArrayItem(property.Value)) return true;
        }

        return false;
    }

    private static bool TryTrimNamedArray(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is not JsonArray array || array.Count == 0) return false;
        if (TrimOneArrayItem(array[^1]!)) return true;
        if (!CanTrimLastArrayItem(propertyName, array.Count)) return false;
        array.RemoveAt(array.Count - 1);
        return true;
    }

    private static bool CanTrimLastArrayItem(string propertyName, int count) =>
        propertyName is not ("members" or "diagnostics" or "callSites") || count > 1;

    private static bool TrimArrayItem(JsonArray values)
    {
        for (var index = values.Count - 1; index >= 0; index--)
        {
            if (values[index] is not null && TrimOneArrayItem(values[index]!)) return true;
        }

        return false;
    }

    private static bool TrimOneLongString(JsonNode node, bool includeStablePaths) => node switch
    {
        JsonObject obj => TrimObjectLongString(obj, includeStablePaths),
        JsonArray values => TrimArrayLongString(values, includeStablePaths),
        _ => false,
    };

    private static bool TrimObjectLongString(JsonObject obj, bool includeStablePaths)
    {
        foreach (var property in obj)
        {
            if (TryTrimStringProperty(obj, property, includeStablePaths)) return true;
            if (property.Value is not null && TrimOneLongString(property.Value, includeStablePaths)) return true;
        }

        return false;
    }

    private static bool TryTrimStringProperty(
        JsonObject obj,
        KeyValuePair<string, JsonNode?> property,
        bool includeStablePaths)
    {
        if (property.Value is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || text is null
            || text.Length <= 128
            || (!includeStablePaths && IsStablePathProperty(property.Key)))
        {
            return false;
        }

        obj[property.Key] = text[..125] + "…";
        return true;
    }

    private static bool TrimArrayLongString(JsonArray values, bool includeStablePaths)
    {
        foreach (var value in values)
        {
            if (value is not null && TrimOneLongString(value, includeStablePaths)) return true;
        }

        return false;
    }

    private static bool IsStablePathProperty(string propertyName) => propertyName is
        "assemblyPath" or "targetPath" or "resolvedPath" or "sourceProjectPath" or
        "generatedDocumentPath" or "solutionPath" or "repositoryUrl" or "loadedRevision";

    private static bool RemoveOneOptionalProperty(JsonNode node)
    {
        if (RemoveOneDeepDetail(node)) return true;
        return node switch
        {
            JsonObject obj => RemoveFromObject(obj),
            JsonArray values => RemoveFromArray(values),
            _ => false,
        };
    }

    private static bool RemoveFromObject(JsonObject obj)
    {
        foreach (var propertyName in OptionalPropertyNames)
        {
            if (TryRemoveOptionalProperty(obj, propertyName)) return true;
        }

        foreach (var property in obj)
        {
            if (property.Value is not null && RemoveOneOptionalProperty(property.Value)) return true;
        }

        return false;
    }

    private static bool TryRemoveOptionalProperty(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is JsonArray array)
        {
            if (array.Count <= 1) return false;
            array.RemoveAt(array.Count - 1);
            return true;
        }

        return obj.Remove(propertyName);
    }

    private static bool RemoveFromArray(JsonArray values)
    {
        foreach (var value in values)
        {
            if (value is not null && RemoveOneOptionalProperty(value)) return true;
        }

        return false;
    }

    private static bool RemoveOneDeepDetail(JsonNode node) => node switch
    {
        JsonObject obj => RemoveDeepDetailFromObject(obj),
        JsonArray values => RemoveDeepDetailFromArray(values),
        _ => false,
    };

    private static bool RemoveDeepDetailFromObject(JsonObject obj)
    {
        foreach (var propertyName in DeepDetailPropertyNames)
        {
            if (obj.Remove(propertyName)) return true;
        }

        foreach (var property in obj)
        {
            if (property.Value is not null && RemoveOneDeepDetail(property.Value)) return true;
        }

        return false;
    }

    private static bool RemoveDeepDetailFromArray(JsonArray values)
    {
        foreach (var value in values)
        {
            if (value is not null && RemoveOneDeepDetail(value)) return true;
        }

        return false;
    }
}
