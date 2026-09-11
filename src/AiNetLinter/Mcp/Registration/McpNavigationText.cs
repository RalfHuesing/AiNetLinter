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

    /// <summary>
    /// Assembly tools expose their navigation metadata as a durable Markdown footer. Unlike the
    /// compact generic suffix this remains present for complete responses too, because an
    /// assembly result can be followed by source-backed or decompiled navigation.
    /// </summary>
    internal static string FormatAssembly(McpNavigationPayload navigation)
    {
        var action = navigation.Next?.Action;
        var lines = new[]
        {
            "## Navigation",
            $"- status: operation=`{navigation.Status.Operation}`, completeness=`{navigation.Status.Completeness}`",
            $"- completeness: `{navigation.Status.Completeness}`",
        };
        return string.IsNullOrWhiteSpace(action)
            ? string.Join("\n", lines)
            : string.Join("\n", lines) + $"\n- next: `{navigation.Next!.Kind}` — {NormalizeSingleLine(action)}";
    }

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
        var scopeName = FindEmptyScopeName(scope, structured) ?? "angeforderter Scope";
        var count = FindEmptyScopeCount(structured);
        var countText = count is null ? "keine Treffer" : $"0 von {count.Value} Treffern";
        return $"keine Treffer im Scope {scopeName} ({countText}).";
    }

    private static string? FindEmptyScopeName(JsonObject? scope, JsonElement? structured) =>
        ReadString(scope, "effectiveScope")
        ?? ReadString(scope, "requestedType")
        ?? ReadString(scope, "scope")
        ?? ReadString(structured, "scope", "effectiveScope")
        ?? ReadString(structured, "scope", "requestedType")
        ?? ReadString(structured, "scope", "scope")
        ?? ReadString(structured, "effectiveScope")
        ?? ReadString(structured, "summary", "scope")
        ?? ReadString(structured, "summary", "effectiveScope")
        ?? ReadString(structured, "scopeType");

    private static int? FindEmptyScopeCount(JsonElement? structured)
    {
        var paths = new[]
        {
            new[] { "totalCount" }, new[] { "totalMatches" }, new[] { "totalViolations" },
            new[] { "totalViolationsOnFile" }, new[] { "summary", "total" },
            new[] { "summary", "totalCount" }, new[] { "summary", "totalMatches" },
            new[] { "summary", "totalViolations" }, new[] { "summary", "totalCandidates" },
            new[] { "summary", "totalClusters" }, new[] { "completeness", "totalCount" },
            new[] { "completeness", "totalMatches" }, new[] { "completeness", "totalViolations" },
            new[] { "completeness", "totalMatchedLineCount" }, new[] { "completeness", "totalCallSiteCount" },
        };
        foreach (var path in paths)
        {
            var count = ReadCount(structured, path);
            if (count is not null) return count;
        }
        return null;
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
