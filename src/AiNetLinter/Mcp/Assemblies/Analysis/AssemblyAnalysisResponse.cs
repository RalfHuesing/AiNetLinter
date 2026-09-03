#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Output;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisResponseRequest(
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null);

internal static class AssemblyAnalysisResponse
{
    internal static bool FitsResponseBudget(CallToolResult result, AssemblyAnalysisLease lease, int responseBudgetBytes = 0)
    {
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            responseBudgetBytes,
            null,
            lease.Context.ResponseBudgetBytes);
        return Measure(CreateEnriched(result, lease)).TotalBytes <= budget;
    }

    internal static CallToolResult Enrich(CallToolResult result, AssemblyAnalysisLease lease)
        => Enrich(result, lease, new AssemblyAnalysisResponseRequest());

    internal static CallToolResult Enrich(
        CallToolResult result,
        AssemblyAnalysisLease lease,
        AssemblyAnalysisResponseRequest request)
    {
        var enriched = CreateEnriched(result, lease);
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            request.MaxResponseBytes,
            request.DetailLevel,
            lease.Context.ResponseBudgetBytes);
        return ApplyWireBudget(enriched, budget, AssemblyPaging.ReadOffset(request.Cursor));
    }

    private static CallToolResult CreateEnriched(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(
            lease.Context.Diagnostics
                .Concat(lease.ReferenceExpansionDiagnostics)
                .ToArray());
        var metadata = new AssemblyResponseMetadata(
            lease.CanonicalPath,
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            effectiveStatus.ToWireValue(),
            effectiveStatus.ToCompletenessLabel(),
            origin.SourceSnapshotIdentity,
            origin.FallbackReason,
            CreateSourceDiagnosticsSummary(origin.SourceDiagnostics),
            origin.BodyAvailability,
            origin.ContentMode);

        JsonElement? structured = result.StructuredContent;
        if (structured is { ValueKind: JsonValueKind.Object })
        {
            var node = JsonNode.Parse(structured.Value.GetRawText()) as JsonObject ?? new JsonObject();
            node["analysis"] = JsonSerializer.SerializeToNode(metadata, McpJsonOptions.Default);
            structured = JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
        }

        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? new TextContentBlock
                {
                    Text = FormatHeader(metadata) + text.Text,
                }
                : block)
            .ToList();

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = content,
            StructuredContent = structured,
        };
    }

    internal static CallToolResult ApplyWireBudget(CallToolResult result, int budget, int cursorOffset)
    {
        var withBudget = AddWireBudgetMetadata(
            result,
            budget,
            IsStructuredTruncated(result.StructuredContent));
        if (Measure(withBudget).TotalBytes <= budget) return withBudget;

        withBudget = ReplaceText(
            withBudget,
            "[ASSEMBLY] StructuredContent ist die kanonische Nutzlast; " +
            "die Textdarstellung wurde wegen des gemeinsamen Wire-Budgets gekürzt.");
        withBudget = AddWireBudgetMetadata(withBudget, budget, isTruncated: true);

        for (var attempt = 0; attempt < 8 && Measure(withBudget).TotalBytes > budget; attempt++)
        {
            if (withBudget.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
            {
                withBudget = ReplaceText(withBudget, TrimUtf8(
                    withBudget.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty,
                    budget));
                break;
            }

            var available = Math.Max(1, budget - Measure(withBudget).TextBytes);
            var trimmed = TrimStructured(structured, available, cursorOffset);
            withBudget = ReplaceStructured(withBudget, trimmed);
            withBudget = AddWireBudgetMetadata(withBudget, budget, isTruncated: true);
        }

        if (Measure(withBudget).TotalBytes > budget)
        {
            withBudget = ReplaceStructured(withBudget, JsonSerializer.SerializeToElement(new JsonObject
            {
                ["wireTruncated"] = true,
                ["truncatedBy"] = new JsonArray("responseBudget"),
            }, McpJsonOptions.Default));
            withBudget = AddWireBudgetMetadata(withBudget, budget, isTruncated: true);
        }

        return withBudget;
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
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var measurement = Measure(candidate);
            node["wireBudget"] = new JsonObject
            {
                ["limitBytes"] = budget,
                ["textBytes"] = measurement.TextBytes,
                ["structuredBytes"] = measurement.StructuredBytes,
                ["totalBytes"] = measurement.TotalBytes,
                ["truncated"] = isTruncated,
            };
            var next = ReplaceStructured(candidate, JsonSerializer.SerializeToElement(node, McpJsonOptions.Default));
            if (Measure(next) == measurement) return next;
            candidate = next;
        }

        return candidate;
    }

    private static JsonElement TrimStructured(JsonElement structured, int budget, int cursorOffset)
    {
        var node = JsonNode.Parse(structured.GetRawText()) ?? new JsonObject();
        MarkStructuredTruncated(node);
        while (JsonSerializer.SerializeToUtf8Bytes(node, McpJsonOptions.Default).Length > budget
            && TryTrimNode(node))
        {
            AssemblyAnalysisResponseEnvelope.RecalculateEnvelopes(node, cursorOffset);
        }

        AssemblyAnalysisResponseEnvelope.RecalculateEnvelopes(node, cursorOffset);

        return JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
    }

    private static bool IsStructuredTruncated(JsonElement? structured) =>
        structured is { ValueKind: JsonValueKind.Object } value
        && ((value.TryGetProperty("wireTruncated", out var wireTruncated)
             && wireTruncated.ValueKind == JsonValueKind.True)
            || (value.TryGetProperty("isTruncated", out var isTruncated)
                && isTruncated.ValueKind == JsonValueKind.True)
            || (value.TryGetProperty("truncated", out var truncated)
                && truncated.ValueKind == JsonValueKind.True));

    private static bool TryTrimNode(JsonNode node) =>
        node switch
        {
            JsonObject obj => TryTrimObject(obj),
            JsonArray array => TryTrimArray(array),
            _ => false,
        };

    private static bool TryTrimObject(JsonObject obj) =>
        TryTrimOptionalSections(obj)
        || TryTrimCollections(obj)
        || TryTrimObjectStrings(obj)
        || TryTrimObjectChildren(obj)
        || TryRemoveLargestObjectProperty(obj);

    private static bool TryTrimCollections(JsonObject obj)
    {
        foreach (var collectionName in new[] { "types", "extensions" })
        {
            if (obj[collectionName] is not JsonArray collection || collection.Count <= 1) continue;
            collection.RemoveAt(collection.Count - 1);
            return true;
        }

        return false;
    }

    private static bool TryTrimOptionalSections(JsonObject obj)
    {
        foreach (var section in new[] { "body", "classStructure", "metrics", "impact", "callers" })
        {
            if (obj[section] is not { } original
                || original is JsonObject sectionObject && sectionObject["status"] is not null) continue;

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
            if (IsBudgetMetadata(property.Key)) continue;
            if (property.Value is JsonValue value
                && value.TryGetValue<string>(out var text)
                && text.Length > 256)
            {
                obj[property.Key] = TrimUtf8(text, 256);
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
            if (property.Value is not null && TryTrimNode(property.Value)) return true;
        }

        return false;
    }

    private static bool TryRemoveLargestObjectProperty(JsonObject obj)
    {
        var removable = obj
            .Where(property => !IsBudgetMetadata(property.Key) && !IsEnvelopeMetadata(property.Key))
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

        if (array.Count == 0) return false;
        array.RemoveAt(array.Count - 1);
        return true;
    }

    private static void MarkStructuredTruncated(JsonNode node)
    {
        if (node is not JsonObject obj) return;
        if (obj["isTruncated"] is not null) obj["isTruncated"] = true;
        if (obj["truncated"] is not null) obj["truncated"] = true;
        obj["wireTruncated"] = true;
        AssemblyAnalysisResponseEnvelope.AddReason(obj, "responseBudget");
    }

    private static bool IsBudgetMetadata(string name) =>
        name is "analysis" or "wireBudget" or "wireTruncated" or "truncatedBy";

    private static bool IsEnvelopeMetadata(string name) =>
        name is "totalTypes" or "totalExtensions" or "totalCount" or "returnedCount"
            or "shownCount" or "isTruncated" or "truncated" or "continuationToken"
            or "types" or "extensions" or "id";

    private static CallToolResult ReplaceStructured(CallToolResult result, JsonElement structured) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = structured,
        };

    private static CallToolResult ReplaceText(CallToolResult result, string text) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content
                .Select(block => block is TextContentBlock
                    ? new TextContentBlock { Text = text }
                    : block)
                .ToList(),
            StructuredContent = result.StructuredContent,
        };

    private static WireBudgetMeasurement Measure(CallToolResult result)
    {
        var textBytes = result.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = result.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return new(textBytes, structuredBytes);
    }

    private static string TrimUtf8(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;
        var limit = Math.Max(1, maxBytes - Encoding.UTF8.GetByteCount("…"));
        while (limit > 0 && Encoding.UTF8.GetByteCount(value[..limit]) > maxBytes - Encoding.UTF8.GetByteCount("…"))
        {
            limit--;
        }

        return value[..limit] + "…";
    }

    internal static CallToolResult Unsupported(string canonicalPath)
    {
        var result = McpToolResults.Recoverable(
            LinterErrorCodes.AssemblyTargetUnsupported,
            "Dieses Tool unterstützt das Assembly-Ziel nicht.",
            new McpErrorParameters(
                Context: canonicalPath,
                Hint: "Für dieses Assembly-Ziel eine unterstützte Roslyn-Abfrage oder targetType='project' verwenden.",
                TargetType: "assembly",
                TargetPath: canonicalPath));
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock
            {
                Text = "[ASSEMBLY] capability=unsupported; status=unsupported; " +
                       "origin=assembly-target\n\n" + result.Content.OfType<TextContentBlock>().Single().Text,
            }],
            StructuredContent = result.StructuredContent,
        };
    }

    private static string FormatHeader(AssemblyResponseMetadata metadata) =>
        $"[ASSEMBLY] targetType=assembly; targetPath={metadata.TargetPath}; origin={metadata.Origin}; " +
        $"sourcePath={metadata.SourcePath ?? "none"}; snapshot={FormatSnapshot(metadata.SourceSnapshot)}; " +
        $"confidence={metadata.Confidence}; trust={metadata.Trust}; generation={metadata.Generation}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}; " +
        $"fallbackReason={metadata.FallbackReason ?? "none"}; bodyAvailability={metadata.BodyAvailability}; " +
        $"contentMode={metadata.ContentMode}; sourceDiagnostics={metadata.SourceDiagnosticsSummary.ShownCount}/" +
        $"{metadata.SourceDiagnosticsSummary.TotalCount}\n\n";

    private static AssemblySourceDiagnosticsSummary CreateSourceDiagnosticsSummary(
        IReadOnlyList<ExternalSourceConfigurationDiagnostic>? diagnostics)
    {
        var source = diagnostics ?? [];
        var samples = source
            .Take(5)
            .Select(diagnostic => $"{diagnostic.Code}: {AssemblyAnalysisResponseLimits.NormalizeForDisplay(diagnostic.Message)}")
            .ToArray();
        return new(source.Count, samples.Length, source.Count > samples.Length, samples);
    }

    private static string FormatSnapshot(SourceSnapshotIdentity? snapshot) =>
        snapshot is null ? "none" : $"{snapshot.RepositoryUrl}@{snapshot.LoadedRevision}";

    private sealed record AssemblyResponseMetadata(
        string TargetPath,
        string Origin,
        string? SourcePath,
        string AssemblyHash,
        string GeneratedPath,
        string Confidence,
        string Trust,
        long Generation,
        string Status,
        string Completeness,
        SourceSnapshotIdentity? SourceSnapshot,
        string? FallbackReason,
        AssemblySourceDiagnosticsSummary SourceDiagnosticsSummary,
        string BodyAvailability,
        string ContentMode)
    {
        public string TargetType => "assembly";
    }

    private sealed record AssemblySourceDiagnosticsSummary(
        int TotalCount,
        int ShownCount,
        bool Truncated,
        IReadOnlyList<string> Samples);

    private readonly record struct WireBudgetMeasurement(int TextBytes, int StructuredBytes)
    {
        internal int TotalBytes => TextBytes + StructuredBytes;
    }
}
