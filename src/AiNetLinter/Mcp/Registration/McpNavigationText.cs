#nullable enable

using System;
using System.Text.Json.Nodes;

namespace AiNetLinter.Mcp.Registration;

using AiNetLinter.Mcp;

/// <summary>Knappes Textstatus-Suffix des gemeinsamen Navigationsaggregats.</summary>
internal static class McpNavigationText
{
    internal static string Format(McpNavigationPayload navigation)
    {
        var operation = navigation.Status.Operation;
        var completeness = navigation.Status.Completeness;
        if (operation == "ok" && completeness == "complete") return string.Empty;

        var status = $"Status: operation={operation}, completeness={completeness}, analysisQuality={navigation.Analysis.Quality}";
        if (completeness == "empty")
        {
            return $"{status}; {FormatEmptyScope(navigation.Scope)}";
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

    private static string FormatEmptyScope(JsonObject? scope)
    {
        var scopeName = FindEmptyScopeName(scope) ?? "angeforderter Scope";
        return $"keine Treffer im Scope {scopeName} (keine Treffer).";
    }

    private static string? FindEmptyScopeName(JsonObject? scope) =>
        ReadString(scope, "effectiveScope")
        ?? ReadString(scope, "requestedType")
        ?? ReadString(scope, "scope");

    private static string? ReadString(JsonObject? owner, string propertyName) =>
        owner?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string NormalizeSingleLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
