#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

/// <summary>Projects complete assembly result units only after the navigation envelope is present.</summary>
internal static class AssemblyAnalysisPostNavigationResponseBudget
{
    internal static CallToolResult ApplyInspect(CallToolResult result, int maxResponseBytes, bool publicOnly)
    {
        if (McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        if (!HasProperty(result, "types")) return result;
        var payload = JsonSerializer.Deserialize<InspectAssemblyPayload>(result.StructuredContent?.GetRawText() ?? string.Empty, McpJsonOptions.Default);
        if (payload is null) return result;

        var header = Header(result);
        var projected = AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            publicOnly,
            candidate => McpResponseSize.From(CreateInspect(MarkTruncated(candidate), header, publicOnly)).TotalBytes <= maxResponseBytes);
        var candidate = CreateInspect(MarkTruncated(projected), header, publicOnly);
        return McpResponseSize.From(candidate).TotalBytes <= maxResponseBytes
            ? candidate
            : TooSmall(maxResponseBytes, McpResponseSize.From(candidate).TotalBytes);
    }

    internal static CallToolResult ApplyExtensions(CallToolResult result, int maxResponseBytes)
    {
        if (McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        if (!HasProperty(result, "extensions")) return result;
        var payload = JsonSerializer.Deserialize<FindAssemblyExtensionsPayload>(result.StructuredContent?.GetRawText() ?? string.Empty, McpJsonOptions.Default);
        if (payload is null) return result;

        var header = Header(result);
        var projected = AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            candidate => McpResponseSize.From(CreateExtensions(MarkTruncated(candidate), header)).TotalBytes <= maxResponseBytes);
        var candidate = CreateExtensions(MarkTruncated(projected), header);
        return McpResponseSize.From(candidate).TotalBytes <= maxResponseBytes
            ? candidate
            : TooSmall(maxResponseBytes, McpResponseSize.From(candidate).TotalBytes);
    }

    internal static CallToolResult ApplyContext(CallToolResult result, int maxResponseBytes)
    {
        if (McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        if (!HasProperty(result, "contextId")) return result;
        var payload = JsonSerializer.Deserialize<ContextPayload>(result.StructuredContent?.GetRawText() ?? string.Empty, McpJsonOptions.Default);
        if (payload is null) return result;

        var header = Header(result);
        var projected = payload;
        while (TryDropSection(projected, out var reduced))
        {
            projected = MarkTruncated(reduced);
            var candidate = CreateContext(projected, header);
            if (McpResponseSize.From(candidate).TotalBytes <= maxResponseBytes) return candidate;
        }

        projected = MarkTruncated(projected);
        return TooSmall(maxResponseBytes, McpResponseSize.From(CreateContext(projected, header)).TotalBytes);
    }

    private static CallToolResult CreateInspect(InspectAssemblyPayload payload, string header, bool publicOnly) =>
        McpToolResults.Text(header + InspectAssemblyFormatter.FormatText(payload, publicOnly), payload);

    private static CallToolResult CreateExtensions(FindAssemblyExtensionsPayload payload, string header) =>
        McpToolResults.Text(header + Responses.FindAssemblyExtensionsResponseBuilder.FormatText(payload), payload);

    private static CallToolResult CreateContext(ContextPayload payload, string header) =>
        McpToolResults.Text(header + FormatContext(payload), payload);

    private static string Header(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        var separator = text.IndexOf("\n\n", StringComparison.Ordinal);
        return separator < 0 ? string.Empty : text[..(separator + 2)];
    }

    private static bool HasProperty(CallToolResult result, string propertyName) =>
        result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
        && structured.TryGetProperty(propertyName, out _);

    private static CallToolResult TooSmall(int requested, int minimum) =>
        McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={requested} ist zu klein für die fachliche Mindestprojektion; Mindestwert: {minimum} Bytes.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimum} setzen; die Antwort wird nur an vollständigen Facheinheiten gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: requested,
                MinimumResponseBytes: minimum));

    private static bool TryDropSection(ContextPayload payload, out ContextPayload reduced)
    {
        foreach (var section in OptionalSections)
        {
            if (payload.Section(section) is null) continue;
            reduced = payload.Without(section);
            return true;
        }
        reduced = payload;
        return false;
    }

    private static ContextPayload MarkTruncated(ContextPayload payload) => payload with
    {
        IsTruncated = true,
        TruncatedBy = payload.TruncatedBy?.Contains("responseBudget", StringComparer.Ordinal) == true
            ? payload.TruncatedBy
            : (payload.TruncatedBy ?? Array.Empty<string>()).Append("responseBudget").ToList(),
        Navigation = payload.Navigation is null ? null : payload.Navigation with
        {
            Status = payload.Navigation.Status with { Completeness = "truncated" },
            Next = new McpNavigationNext("request_detail", "maxResponseBytes erhöhen oder einen Abschnitt gezielt erneut anfordern."),
        },
    };

    private static InspectAssemblyPayload MarkTruncated(InspectAssemblyPayload payload) => payload with
    {
        Navigation = MarkNavigation(payload.Navigation),
    };

    private static FindAssemblyExtensionsPayload MarkTruncated(FindAssemblyExtensionsPayload payload) => payload with
    {
        Navigation = MarkNavigation(payload.Navigation),
    };

    private static McpNavigationPayload? MarkNavigation(McpNavigationPayload? navigation) =>
        navigation is null ? null : navigation with
        {
            Status = navigation.Status with { Completeness = "truncated" },
            Next = new McpNavigationNext("request_detail", "maxResponseBytes erhöhen oder die Abfrage gezielt verfeinern."),
        };

    private static string FormatContext(ContextPayload payload)
    {
        var lines = new List<string>
        {
            $"Assembly-Kontext: {payload.ReturnedCount} von {payload.TotalCount}",
            $"Scope: {payload.Scope}; Vollständigkeit: {payload.Completeness}",
        };
        if (!string.IsNullOrWhiteSpace(payload.SymbolIdentifier)) lines.Add($"Symbol: {payload.SymbolIdentifier}");
        foreach (var section in OptionalSections.Reverse())
        {
            if (payload.Section(section) is not null) lines.Add($"Abschnitt: {section}");
        }
        if (payload.IsTruncated) lines.Add("Antwort gekürzt; maxResponseBytes erhöhen oder den gewünschten Abschnitt gezielt erneut anfordern.");
        return string.Join("\n", lines);
    }

    private static readonly string[] OptionalSections = ["impact", "callers", "classStructure", "body", "metrics", "assemblyAnalysis"];

    private sealed record ContextPayload(
        string ContextId,
        string TargetPath,
        string Scope,
        string Completeness,
        string? SymbolIdentifier,
        JsonElement? Identity,
        JsonElement? Origin,
        JsonElement? AssemblyAnalysis,
        JsonElement? Metrics,
        JsonElement? Body,
        JsonElement? ClassStructure,
        JsonElement? Callers,
        JsonElement? Impact,
        int TotalCount,
        int ReturnedCount,
        bool IsTruncated,
        string? ContinuationToken,
        IReadOnlyList<string>? TruncatedBy,
        McpNavigationPayload? Navigation = null)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }

        internal JsonElement? Section(string name) => name switch
        {
            "assemblyAnalysis" => AssemblyAnalysis,
            "metrics" => Metrics,
            "body" => Body,
            "classStructure" => ClassStructure,
            "callers" => Callers,
            "impact" => Impact,
            _ => null,
        };

        internal ContextPayload Without(string name) => name switch
        {
            "assemblyAnalysis" => this with { AssemblyAnalysis = null },
            "metrics" => this with { Metrics = null },
            "body" => this with { Body = null },
            "classStructure" => this with { ClassStructure = null },
            "callers" => this with { Callers = null },
            "impact" => this with { Impact = null },
            _ => this,
        };
    }
}
