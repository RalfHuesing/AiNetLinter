#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiNetLinter.Mcp.Registration;

using AiNetLinter.Mcp;

/// <summary>Knappes Textstatus-Suffix des gemeinsamen Navigationsaggregats.</summary>
internal static class McpNavigationText
{
    internal static string Format(McpNavigationPayload navigation) => Format(navigation, null);

    internal static string Format(McpNavigationPayload navigation, JsonElement? structured)
    {
        var operation = navigation.Status.Operation;
        var completeness = navigation.Status.Completeness;
        if (operation == "ok" && completeness == "complete") return string.Empty;

        var status = $"Status: operation={operation}, completeness={completeness}";
        if (completeness == "empty")
        {
            return $"{status}; {FormatEmptyScope(navigation.Scope, structured)}";
        }

        var action = navigation.Next?.Action
            ?? "Scope oder Detaillevel prüfen und die Antwort gezielt wiederholen.";
        if (completeness == "truncated"
            && action.StartsWith("Scope oder Detaillevel verfeinern", StringComparison.Ordinal))
        {
            action = "maxResults erhöhen oder Scope verfeinern und die Antwort gezielt wiederholen.";
        }
        return $"{status}; Aktion: {NormalizeSingleLine(action)}";
    }

    private static string FormatEmptyScope(JsonObject? scope, JsonElement? structured)
    {
        var scopeName = ReadString(scope, "effectiveScope")
            ?? ReadString(scope, "requestedType")
            ?? ReadString(scope, "scope")
            ?? ReadString(structured, "scope", "effectiveScope")
            ?? ReadString(structured, "scope", "requestedType")
            ?? ReadString(structured, "scope", "scope")
            ?? ReadString(structured, "effectiveScope")
            ?? ReadString(structured, "summary", "scope")
            ?? ReadString(structured, "summary", "effectiveScope")
            ?? ReadString(structured, "scopeType")
            ?? "angeforderter Scope";
        var count = ReadCount(structured, "totalCount")
            ?? ReadCount(structured, "totalMatches")
            ?? ReadCount(structured, "totalViolations")
            ?? ReadCount(structured, "totalViolationsOnFile")
            ?? ReadCount(structured, "summary", "total")
            ?? ReadCount(structured, "summary", "totalCount")
            ?? ReadCount(structured, "summary", "totalMatches")
            ?? ReadCount(structured, "summary", "totalViolations")
            ?? ReadCount(structured, "summary", "totalCandidates")
            ?? ReadCount(structured, "summary", "totalClusters")
            ?? ReadCount(structured, "completeness", "totalCount")
            ?? ReadCount(structured, "completeness", "totalMatches")
            ?? ReadCount(structured, "completeness", "totalViolations")
            ?? ReadCount(structured, "completeness", "totalMatchedLineCount")
            ?? ReadCount(structured, "completeness", "totalCallSiteCount");
        var countText = count is null ? "keine Treffer" : $"0 von {count.Value} Treffern";
        return $"keine Treffer im Scope {scopeName} ({countText}).";
    }

    private static string? ReadString(JsonObject? owner, string propertyName) =>
        owner?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string? ReadString(JsonElement? owner, params string[] path)
    {
        if (owner is not { ValueKind: JsonValueKind.Object } value) return null;

        foreach (var propertyName in path)
        {
            if (!value.TryGetProperty(propertyName, out var nested)) return null;
            value = nested;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? ReadCount(JsonElement? owner, params string[] path)
    {
        if (owner is not { ValueKind: JsonValueKind.Object } value) return null;

        foreach (var propertyName in path)
        {
            if (!value.TryGetProperty(propertyName, out value)) return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count)
            ? count
            : null;
    }

    private static string NormalizeSingleLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
