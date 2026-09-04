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
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisResponseRequest(
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null);
internal static partial class AssemblyAnalysisResponse
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
        if (AssemblyAnalysisResponseLimits.IsBelowMinimumResponseBudget(request.MaxResponseBytes))
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss mindestens {AssemblyAnalysisResponseLimits.MinimumResponseBytes} Bytes betragen, damit ein maschinenlesbarer Assembly-Envelope mit Status und Budgetdaten repräsentierbar bleibt.",
                $"maxResponseBytes erhöhen oder den Parameter weglassen; dann gilt das konfigurierte Assembly-Budget von {lease.Context.ResponseBudgetBytes} Bytes.");
        }

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
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            lease.Context.Generation,
            effectiveStatus.ToWireValue(),
            effectiveStatus.ToCompletenessLabel(),
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
        if (Measure(withBudget).TotalBytes <= budget) return withBudget;

        for (var attempt = 0; attempt < 128 && Measure(withBudget).TotalBytes > budget; attempt++)
        {
            if (withBudget.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
            {
                var text = withBudget.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
                withBudget = ReplaceText(withBudget, TrimUtf8(text, Math.Max(1, budget - Measure(withBudget).StructuredBytes)));
                break;
            }

            var available = Math.Max(1, budget - Measure(withBudget).TextBytes);
            var trimmed = TrimStructured(structured, available, cursorOffset);
            if (trimmed.GetRawText() == structured.GetRawText())
            {
                var text = withBudget.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
                var remainingForText = Math.Max(1, budget - Measure(withBudget).StructuredBytes);
                withBudget = ReplaceText(withBudget, TrimUtf8(text, remainingForText));
                break;
            }
            withBudget = ReplaceStructured(withBudget, trimmed);
            withBudget = AddWireBudgetMetadata(withBudget, budget, isTruncated: true);
        }

        if (Measure(withBudget).TotalBytes > budget)
        {
            withBudget = ReplaceStructured(withBudget, JsonSerializer.SerializeToElement(new JsonObject
            {
                ["isTruncated"] = true,
                ["truncated"] = true,
                ["wireTruncated"] = true,
                ["truncatedBy"] = new JsonArray("responseBudget"),
                ["detailHint"] = "Die strukturierte Nutzlast wurde auf den minimalen Antwortumfang gekürzt; maxResponseBytes erhöhen oder die Detailabfrage gezielt erneut anfordern.",
            }, McpJsonOptions.Default));
            var text = withBudget.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
            var remainingForText = Math.Max(1, budget - Measure(withBudget).StructuredBytes);
            withBudget = ReplaceText(withBudget, TrimUtf8(text, remainingForText));
            withBudget = AddWireBudgetMetadata(withBudget, budget, isTruncated: true);
        }

        withBudget = AddWireBudgetMetadata(withBudget, budget, IsStructuredTruncated(withBudget.StructuredContent));
        if (Measure(withBudget).TotalBytes <= budget) return withBudget;

        return McpToolResults.InvalidArgument(
            $"Das Assembly-Antwortbudget von {budget} Bytes ist für den minimalen Wire-Envelope nicht repräsentierbar.",
            $"maxResponseBytes auf mindestens {AssemblyAnalysisResponseLimits.MinimumResponseBytes} Bytes erhöhen.");
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
            if (Measure(next) == measurement
                && next.StructuredContent?.GetRawText() == candidate.StructuredContent?.GetRawText()) return next;
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
            // Projection is intentionally batched; envelope fields are rebuilt once below.
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
        foreach (var section in new[] { "body", "classStructure", "metrics", "impact", "callers" })
        {
            if (obj[section] is not { } original
                || original is JsonObject sectionObject
                    && (sectionObject["status"] is not null
                        || ResultCollections.Any(collection => sectionObject[collection] is JsonArray))) continue;

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
        name is "analysis" or "wireBudget" or "wireTruncated" or "truncatedBy";

    private static bool IsEnvelopeMetadata(string name) =>
        name is "totalTypes" or "totalExtensions" or "totalCount" or "returnedCount"
            or "shownCount" or "isTruncated" or "truncated" or "continuationToken"
            or "types" or "extensions" or "id" or "status" or "detailHint"
            || name.EndsWith("Envelope", StringComparison.Ordinal);

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

    internal static string TrimUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        const string ellipsis = "…";
        var ellipsisBytes = Encoding.UTF8.GetByteCount(ellipsis);
        var targetBytes = maxBytes - ellipsisBytes;

        if (targetBytes < 0)
        {
            return maxBytes >= 1 ? "." : string.Empty;
        }

        var limit = Math.Min(value.Length, targetBytes);
        while (limit > 0 && Encoding.UTF8.GetByteCount(value[..limit]) > targetBytes)
        {
            limit--;
        }

        if (limit > 0 && char.IsHighSurrogate(value[limit - 1]))
        {
            limit--;
        }

        return value[..limit] + ellipsis;
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
        $"[ASSEMBLY] targetType=assembly; targetPath={metadata.TargetPath}; generatedPath={metadata.GeneratedPath}; origin={metadata.Origin}; " +
        $"confidence={metadata.Confidence}; generation={metadata.Generation}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}; " +
        $"bodyAvailability={metadata.BodyAvailability}; contentMode={metadata.ContentMode}\n\n";

    private sealed record AssemblyResponseMetadata(
        string TargetPath,
        string Origin,
        string AssemblyHash,
        string GeneratedPath,
        string Confidence,
        long Generation,
        string Status,
        string Completeness,
        string BodyAvailability,
        string ContentMode)
    {
        public string TargetType => "assembly";
    }

    private readonly record struct WireBudgetMeasurement(int TextBytes, int StructuredBytes)
    {
        internal int TotalBytes => TextBytes + StructuredBytes;
    }
}
