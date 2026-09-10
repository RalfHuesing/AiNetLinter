#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Wire;

internal static partial class McpToolResultsWireBudget
{
    private static void CollectArrays(JsonNode? node, string path, ICollection<ArrayCandidate> result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key is "wireBudget" or "truncatedBy") continue;
                    CollectArrays(property.Value, path + "." + property.Key, result);
                }

                break;
            case JsonArray array:
                var largestItemBytes = array
                    .Select(item => item is null ? 0 : MeasureNode(item))
                    .DefaultIfEmpty(0)
                    .Max();
                result.Add(new ArrayCandidate(array, path, largestItemBytes));
                for (var index = 0; index < array.Count; index++)
                {
                    CollectArrays(array[index], path + "[" + index + "]", result);
                }

                break;
        }
    }

    private static void CollectStrings(JsonNode? node, string path, ICollection<StringCandidate> result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key == "wireBudget") continue;
                    if (property.Value is JsonValue value
                        && value.TryGetValue<string>(out var stringValue)
                        && stringValue is not null)
                    {
                        result.Add(new StringCandidate(obj, property.Key, stringValue, path + "." + property.Key));
                    }

                    CollectStrings(property.Value, path + "." + property.Key, result);
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectStrings(array[index], path + "[" + index + "]", result);
                }

                break;
        }
    }

    private static bool IsProtectedWireString(string key) =>
        key.Contains("id", StringComparison.OrdinalIgnoreCase)
        || key.Contains("symbol", StringComparison.OrdinalIgnoreCase)
        || key is "completeness" or "status" or "nextStep" or "semantics" or "evidenceBoundary";

    private static JsonObject CreateTruncatedSection(string sectionName) =>
        new()
        {
            ["completeness"] = "truncated",
            ["isTruncated"] = true,
            ["truncatedBy"] = new JsonArray("responseBudget"),
            ["nextStep"] = BuildSectionNextStep(sectionName),
        };

    private static void MarkSectionTruncated(JsonObject section, string sectionName)
    {
        section["completeness"] = "truncated";
        section["isTruncated"] = true;
        if (section.ContainsKey("status")) section["status"] = "truncated";
        if (sectionName == "testContext") ReconcileTestContextCounts(section);
        AddReason(section, "responseBudget");
        section["nextStep"] = BuildSectionNextStep(sectionName, ReadString(section, "nextStep"));
    }

    private static void ReconcileTestContextCounts(JsonObject section)
    {
        if (section["testFiles"] is not JsonArray testFiles) return;

        var returnedMethods = testFiles
            .OfType<JsonObject>()
            .Sum(file => (file["testMethods"] as JsonArray)?.Count ?? 0);
        if (section.ContainsKey("returnedTestFiles")) section["returnedTestFiles"] = testFiles.Count;
        if (section.ContainsKey("returnedTestMethods")) section["returnedTestMethods"] = returnedMethods;
        if (section.ContainsKey("displayedTestMethods")) section["displayedTestMethods"] = returnedMethods;
    }

    private static void MarkRootTruncated(JsonObject payload, IEnumerable<string> sectionNames)
    {
        var current = ReadString(payload, "completeness");
        if (current is null or "complete" or "empty" or "truncated")
        {
            payload["completeness"] = "truncated";
        }

        payload["isTruncated"] = true;
        if (payload["navigation"] is JsonObject navigation)
        {
            var status = navigation["status"] as JsonObject;
            if (status is not null) status["completeness"] = "truncated";
        }
        AddReason(payload, "responseBudget");
        var sections = string.Join(", ", sectionNames.OrderBy(name => name, StringComparer.Ordinal));
        payload["nextStep"] =
            (sections.Length == 0
                ? "Wire-Budget der Gesamtantwort erreicht"
                : $"Wire-Budget für {sections} erreicht")
            + ": gezielte Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen.";
    }

    private static void AddReason(JsonObject owner, string reason)
    {
        var reasons = owner["truncatedBy"] as JsonArray;
        if (reasons is null)
        {
            reasons = new JsonArray();
            owner["truncatedBy"] = reasons;
        }

        if (!reasons.Any(item => item is JsonValue value
                && value.TryGetValue<string>(out var current)
                && string.Equals(current, reason, StringComparison.Ordinal)))
        {
            reasons.Add(reason);
        }
    }

    private static string BuildSectionNextStep(string sectionName, string? existing = null)
    {
        if (string.IsNullOrWhiteSpace(existing) || existing.Contains("responseBudget", StringComparison.OrdinalIgnoreCase))
        {
            return $"Abschnitt {sectionName}: Wire-Budget erreicht; Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen.";
        }

        if (existing.Contains($"Wire-Budget für Abschnitt {sectionName} erreicht", StringComparison.OrdinalIgnoreCase)
            || existing.Contains($"Abschnitt {sectionName}: Wire-Budget erreicht", StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        return $"{existing} Wire-Budget für Abschnitt {sectionName} erreicht; Detailabfrage gezielt wiederholen.";
    }

    private static string BuildCompositeBudgetHint(ISet<string> sections, bool rootTruncated)
    {
        var affected = sections.Count == 0
            ? "Gesamtantwort"
            : string.Join(", ", sections.OrderBy(section => section, StringComparer.Ordinal));
        return $"- **Wire-Budget:** UTF-8-Payload gekürzt (Gesamtlimit {CompositeWireBudgetBytes} Bytes, Abschnittslimit {CompositeSectionBudgetBytes} Bytes); betroffene Abschnitte: {affected}; Begrenzung: `responseBudget`. " +
               "**Nächster sicherer Schritt:** gezielte Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen.";
    }

    private static string BuildBudgetSafeText(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        string hint,
        int maxBytes)
    {
        var builder = new StringBuilder();
        AppendCompositeTitle(builder, payload);
        AppendNavigationSummary(builder, payload);
        AppendSectionSummaries(builder, payload, sectionNames, rootSectionName);
        builder.AppendLine(hint);
        return McpUtf8BudgetTrimmer.TrimWithoutEllipsis(builder.ToString().TrimEnd(), maxBytes);
    }

    private static void AppendCompositeTitle(StringBuilder builder, JsonObject payload)
    {
        var title = (payload["declaration"] as JsonObject)?["name"]?.GetValue<string>()
            ?? payload["targetSymbol"]?.GetValue<string>();
        builder.AppendLine(title is null
            ? "# Composite-Antwort (Wire-Budget)"
            : $"# Composite-Antwort (Wire-Budget): {title}");
    }

    private static void AppendNavigationSummary(StringBuilder builder, JsonObject payload)
    {
        if (payload["navigation"] is not JsonObject navigation) return;
        var status = navigation["status"] as JsonObject;
        var operationStatus = status is null ? "ok" : ReadString(status, "operation") ?? "ok";
        var completeness = status is null ? "complete" : ReadString(status, "completeness") ?? "complete";
        builder.AppendLine($"- **Navigation:** Status: {operationStatus}; Completeness: {completeness}");
        if (navigation["next"] is not JsonObject next) return;
        var nextKind = ReadString(next, "kind");
        var nextAction = ReadString(next, "action");
        if (!string.IsNullOrWhiteSpace(nextKind) || !string.IsNullOrWhiteSpace(nextAction))
        {
            builder.AppendLine($"- **Navigation next:** {nextKind ?? "none"} — {nextAction ?? ""}");
        }
    }

    private static void AppendSectionSummaries(
        StringBuilder builder,
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName)
    {
        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;
            AppendSectionSummary(builder, sectionName, section);
        }
    }

    private static void AppendSectionSummary(StringBuilder builder, string sectionName, JsonObject section)
    {
        var status = ReadString(section, "completeness") ?? ReadString(section, "status") ?? "complete";
        builder.AppendLine($"- **Abschnitt {sectionName}:** Status: {status}");
        AppendCountSummary(builder, section, sectionName);
        var nextStep = ReadString(section, "nextStep");
        if (!string.IsNullOrWhiteSpace(nextStep))
        {
            builder.AppendLine($"- **Nächster sicherer Schritt ({sectionName}):** {nextStep}");
        }
    }

    private static void AppendCountSummary(StringBuilder builder, JsonObject section, string sectionName)
    {
        switch (sectionName)
        {
            case "declaration":
                AppendDeclarationSummary(builder, section);
                break;
            case "metrics":
                AppendMetricsSummary(builder, section);
                break;
            case "impact":
                builder.AppendLine($"- **Counts:** {CountArray(section, "callSites")} von {ReadInt(section, "totalCallers", 0)} statischen Referenzen/Call-Sites zurückgegeben.");
                break;
            case "violations":
                AppendViolationsSummary(builder, section);
                break;
            case "testContext":
                AppendTestContextSummary(builder, section);
                break;
            default:
                var counts = $"{ReadInt(section, "returnedCount", CountArray(section, "callSites", "violations"))} sichtbare Treffer von {ReadInt(section, "totalCount", 0)}";
                builder.AppendLine($"- **Counts:** {counts}.");
                break;
        }
    }

    private static void AppendDeclarationSummary(StringBuilder builder, JsonObject section)
    {
        var kind = ReadString(section, "kind") ?? "Symbol";
        var accessibility = ReadString(section, "accessibility") ?? "";
        var lineCount = ReadInt(section, "lineCount", 0);
        var memberCount = CountArray(section, "members");
        var memberText = memberCount > 0 ? $"; {memberCount} Member" : "";
        builder.AppendLine($"- **Deklaration:** {kind} ({accessibility}; {lineCount} Zeilen{memberText}).");
    }

    private static void AppendMetricsSummary(StringBuilder builder, JsonObject section)
    {
        var checks = CountArray(section, "checks");
        var status = ReadString(section, "status") ?? ReadString(section, "completeness") ?? "complete";
        builder.AppendLine($"- **Counts:** {checks} Metriken bewertet (Status: {status}).");
    }

    private static void AppendViolationsSummary(StringBuilder builder, JsonObject section)
    {
        var status = ReadString(section, "status") ?? ReadString(section, "completeness") ?? "complete";
        if (status is not ("complete" or "empty" or "truncated"))
        {
            builder.AppendLine($"- **Counts:** nicht entscheidbar (Status: {status}; keine Sauberkeitsaussage).");
            return;
        }

        var total = ReadInt(section, "totalViolationsOnFile", 0);
        builder.AppendLine($"- **Counts:** {CountArray(section, "violations")} von {total} Verstoesse; {ReadInt(section, "violationsOnSymbol", 0)} direkt auf dem Symbol.");
        builder.AppendLine($"- ({total} Verstoesse; Status: {status}).");
    }

    private static void AppendTestContextSummary(StringBuilder builder, JsonObject section)
    {
        var returnedFiles = ReadInt(section, "returnedTestFiles", CountArray(section, "testFiles"));
        var totalFiles = ReadInt(section, "totalTestFiles", 0);
        var returnedMethods = ReadInt(section, "returnedTestMethods", ReadInt(section, "displayedTestMethods", 0));
        var totalMethods = ReadInt(section, "totalMatchingTests", 0);
        builder.AppendLine($"- **Counts:** {returnedFiles} von {totalFiles} Testdateien und {returnedMethods} von {totalMethods} Testmethoden.");
    }

    private static int ReadInt(JsonObject owner, string propertyName, int fallback) =>
        owner[propertyName] is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : fallback;

    private static int CountArray(JsonObject owner, params string[] propertyNames) =>
        propertyNames.Select(name => owner[name] as JsonArray).FirstOrDefault(array => array is not null)?.Count ?? 0;

    private static CallToolResult UpdateCompositeWireBudget(
        CallToolResult result,
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        bool truncated)
    {
        // Reapplying the composite budget happens after navigation is attached.  In that
        // second pass there may be no new trim operation, but an earlier pass may already
        // have truncated the response.  Preserve that fact across the metadata rebuild;
        // otherwise wireBudget.truncated would be reset to false while the payload still
        // represents a truncated response.
        var existingWireTruncated = ReadBoolean(payload, "wireTruncated")
            || (payload["wireBudget"] is JsonObject existingWireBudget
                && ReadBoolean(existingWireBudget, "truncated"));
        var effectiveTruncated = truncated || existingWireTruncated;

        var candidate = result;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var provisional = ReplaceStructured(
                candidate,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            var measurement = MeasureComposite(provisional);
            payload["wireBudget"] = new JsonObject
            {
                ["limitBytes"] = CompositeWireBudgetBytes,
                ["sectionLimitBytes"] = CompositeSectionBudgetBytes,
                ["textBytes"] = measurement.TextBytes,
                ["structuredBytes"] = measurement.StructuredBytes,
                ["totalBytes"] = 0,
                ["truncated"] = effectiveTruncated,
                ["sections"] = BuildSectionBudgetMetadata(payload, sectionNames, rootSectionName),
            };

            if (effectiveTruncated)
            {
                payload["wireTruncated"] = true;
            }

            var next = ReplaceStructured(
                provisional,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            var finalMeasurement = MeasureComposite(next);
            if (payload["wireBudget"] is JsonObject wireBudget)
            {
                wireBudget["textBytes"] = finalMeasurement.TextBytes;
                wireBudget["structuredBytes"] = finalMeasurement.StructuredBytes;
                wireBudget["totalBytes"] = finalMeasurement.TotalBytes;
            }

            next = ReplaceStructured(
                next,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            if (next.StructuredContent?.GetRawText() == candidate.StructuredContent?.GetRawText())
            {
                return next;
            }

            candidate = next;
        }

        return candidate;
    }

    internal static CallToolResult ReapplyCompositeWireBudgetAfterNavigation(CallToolResult result)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var payload = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload is null || payload["wireBudget"] is not JsonObject wireBudget)
        {
            return result;
        }

        var hasFeatureShape = payload.ContainsKey("impact") && payload.ContainsKey("testContext");
        var hasTestContextShape = !hasFeatureShape
            && payload.ContainsKey("testContext")
            && wireBudget["sections"] is JsonObject sections
            && sections.ContainsKey("testContext");
        return hasFeatureShape
            ? ApplyCompositeWireBudget(result, ["declaration", "metrics", "impact", "testContext", "violations"])
            : hasTestContextShape
                ? ApplyCompositeWireBudget(result, ["testContext"], "testContext")
                : result;
    }

    private static JsonObject BuildSectionBudgetMetadata(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName)
    {
        var sections = new JsonObject();
        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;
            sections[sectionName] = new JsonObject
            {
                ["limitBytes"] = CompositeSectionBudgetBytes,
                ["bytes"] = MeasureSectionNode(section, ReferenceEquals(section, payload)),
                ["truncated"] = ReadString(section, "completeness") == "truncated"
                    || ReadBoolean(section, "isTruncated"),
            };
        }

        return sections;
    }

    private static string ReadText(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static string? ReadString(JsonObject owner, string propertyName) =>
        owner[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool ReadBoolean(JsonObject owner, string propertyName) =>
        owner[propertyName] is JsonValue value
        && value.TryGetValue<bool>(out var flag)
        && flag;

    private static int MeasureNode(JsonNode node) =>
        Encoding.UTF8.GetByteCount(node.ToJsonString(McpJsonOptions.Default));

    private static int MeasureSectionNode(JsonNode node, bool rootSection)
    {
        if (!rootSection || node is not JsonObject owner || !owner.ContainsKey("wireBudget"))
        {
            return MeasureNode(node);
        }

        var copy = JsonNode.Parse(owner.ToJsonString(McpJsonOptions.Default))!.AsObject();
        copy.Remove("wireBudget");
        return MeasureNode(copy);
    }

    private static CompositeMeasurement MeasureComposite(CallToolResult result)
    {
        var textBytes = result.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = result.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return new CompositeMeasurement(textBytes, structuredBytes, textBytes + structuredBytes);
    }

    private static CallToolResult ReplaceStructured(CallToolResult result, JsonElement structured) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = structured,
        };

    internal static CallToolResult ReplaceText(CallToolResult result, string text) =>
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

    private sealed record ArrayCandidate(JsonArray Array, string Path, int LargestItemBytes);

    private sealed record StringCandidate(JsonObject Parent, string Key, string Value, string Path);

    private readonly record struct CompositeMeasurement(int TextBytes, int StructuredBytes, int TotalBytes);
}
