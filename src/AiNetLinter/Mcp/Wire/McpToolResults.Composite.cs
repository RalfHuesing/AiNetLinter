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
    internal const int CompositeWireBudgetBytes = 12 * 1024;
    internal const int CompositeSectionBudgetBytes = 4 * 1024;
    internal static CallToolResult ApplyCompositeWireBudget(
        CallToolResult result,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName = null)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var payload = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload is null) return result;

        var truncatedSections = new SortedSet<string>(StringComparer.Ordinal);
        var rootTruncated = TrimSectionsToBudget(payload, sectionNames, rootSectionName, truncatedSections);
        if (rootTruncated) MarkRootTruncated(payload, truncatedSections);
        return ProjectWithinBudget(new BudgetProjectionContext
        {
            Result = result,
            Payload = payload,
            SectionNames = sectionNames,
            RootSectionName = rootSectionName,
            OriginalText = ReadText(result),
            TruncatedSections = truncatedSections,
            RootTruncated = rootTruncated,
        });
    }

    private static bool TrimSectionsToBudget(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        ISet<string> truncatedSections)
    {
        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;
            TrimSectionToBudget(payload, sectionName, rootSectionName, section, truncatedSections);
        }
        return truncatedSections.Count > 0;
    }

    private static void TrimSectionToBudget(
        JsonObject payload,
        string sectionName,
        string? rootSectionName,
        JsonObject section,
        ISet<string> truncatedSections)
    {
        while (MeasureSectionNode(section, string.Equals(sectionName, rootSectionName, StringComparison.Ordinal)) > CompositeSectionBudgetBytes)
        {
            if (TryTrimSectionOnce(section))
            {
                MarkSectionTruncated(section, sectionName);
                truncatedSections.Add(sectionName);
                continue;
            }
            if (!string.Equals(rootSectionName, sectionName, StringComparison.Ordinal))
            {
                payload[sectionName] = CreateTruncatedSection(sectionName);
                MarkSectionTruncated((JsonObject)payload[sectionName]!, sectionName);
                truncatedSections.Add(sectionName);
            }
            break;
        }
    }

    private static CallToolResult ProjectWithinBudget(BudgetProjectionContext context)
    {
        var textLimit = CompositeWireBudgetBytes / 2;
        var projectedText = context.OriginalText;
        for (var attempt = 0; attempt < 4096; attempt++)
        {
            context.TextLimit = textLimit;
            projectedText = BuildProjectedText(context);
            context.Text = projectedText;
            var candidate = CreateBudgetCandidate(context);
            var measurement = MeasureComposite(candidate);
            if (measurement.TotalBytes <= CompositeWireBudgetBytes
                && SectionsFit(context.Payload, context.SectionNames, context.RootSectionName)) return candidate;
            if (TryTrimLargestSection(context.Payload, context.SectionNames, context.RootSectionName, context.TruncatedSections))
            {
                MarkRootTruncated(context.Payload, context.TruncatedSections);
                context.RootTruncated = true;
                continue;
            }
            var nextLimit = Math.Min(textLimit, Math.Max(1, CompositeWireBudgetBytes - measurement.StructuredBytes));
            var nextText = BuildBudgetSafeText(
                context.Payload,
                context.SectionNames,
                context.RootSectionName,
                BuildCompositeBudgetHint(context.TruncatedSections, true),
                nextLimit);
            if (nextText == projectedText && measurement.TotalBytes > CompositeWireBudgetBytes) return candidate;
            textLimit = nextLimit;
            context.RootTruncated = true;
            MarkRootTruncated(context.Payload, context.TruncatedSections);
        }
        return CreateBudgetCandidate(context);
    }

    private static string BuildProjectedText(BudgetProjectionContext context) =>
        context.TruncatedSections.Count > 0 || context.RootTruncated
            ? BuildBudgetSafeText(
                context.Payload,
                context.SectionNames,
                context.RootSectionName,
                BuildCompositeBudgetHint(context.TruncatedSections, context.RootTruncated),
                context.TextLimit)
            : context.OriginalText;

    private static CallToolResult CreateBudgetCandidate(BudgetProjectionContext context) =>
        UpdateCompositeWireBudget(
            ReplaceText(
                ReplaceStructured(context.Result, JsonSerializer.SerializeToElement(context.Payload, McpJsonOptions.Default)),
                context.Text),
            context.Payload,
            context.SectionNames,
            context.RootSectionName,
            context.TruncatedSections.Count > 0 || context.RootTruncated);

    private sealed class BudgetProjectionContext
    {
        internal required CallToolResult Result { get; init; }
        internal required JsonObject Payload { get; init; }
        internal required IReadOnlyList<string> SectionNames { get; init; }
        internal string? RootSectionName { get; init; }
        internal required string OriginalText { get; init; }
        internal required ISet<string> TruncatedSections { get; init; }
        internal bool RootTruncated { get; set; }
        internal int TextLimit { get; set; }
        internal string Text { get; set; } = string.Empty;
    }

    private static JsonObject? FindCompositeSection(JsonObject payload, string sectionName, string? rootSectionName) =>
        string.Equals(sectionName, rootSectionName, StringComparison.Ordinal)
            ? payload
            : payload[sectionName] as JsonObject;

    private static bool SectionsFit(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName) =>
        sectionNames
            .Select(name => FindCompositeSection(payload, name, rootSectionName))
            .Where(section => section is not null)
            .All(section => MeasureSectionNode(
                section!,
                ReferenceEquals(section, payload)) <= CompositeSectionBudgetBytes);

    private static bool TryTrimLargestSection(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        ISet<string> truncatedSections)
    {
        var sections = sectionNames
            .Select(name => (Name: name, Node: FindCompositeSection(payload, name, rootSectionName)))
            .Where(item => item.Node is not null)
            .OrderByDescending(item => MeasureSectionNode(
                item.Node!,
                ReferenceEquals(item.Node, payload)))
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var item in sections)
        {
            var section = item.Node!;
            if (TryTrimSectionOnce(section))
            {
                MarkSectionTruncated(section, item.Name);
                truncatedSections.Add(item.Name);
                return true;
            }

            if (!string.Equals(item.Name, rootSectionName, StringComparison.Ordinal))
            {
                payload[item.Name] = CreateTruncatedSection(item.Name);
                MarkSectionTruncated((JsonObject)payload[item.Name]!, item.Name);
                truncatedSections.Add(item.Name);
                return true;
            }
        }

        return false;
    }

    private static bool TryTrimSectionOnce(JsonNode section)
    {
        var arrays = new List<ArrayCandidate>();
        CollectArrays(section, "$", arrays);
        var candidate = arrays
            .Where(item => item.Array.Count > 0)
            .OrderByDescending(item => item.LargestItemBytes)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is not null)
        {
            candidate.Array.RemoveAt(candidate.Array.Count - 1);
            return true;
        }

        var strings = new List<StringCandidate>();
        CollectStrings(section, "$", strings);
        var stringCandidate = strings
            .Where(item => item.Value.Length > 16 && !IsProtectedWireString(item.Key))
            .OrderByDescending(item => Encoding.UTF8.GetByteCount(item.Value))
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (stringCandidate is not null)
        {
            var currentBytes = Encoding.UTF8.GetByteCount(stringCandidate.Value);
            stringCandidate.Parent[stringCandidate.Key] = McpUtf8BudgetTrimmer.TrimWithoutEllipsis(
                stringCandidate.Value,
                Math.Max(1, currentBytes / 2));
            return true;
        }

        return false;
    }
}
