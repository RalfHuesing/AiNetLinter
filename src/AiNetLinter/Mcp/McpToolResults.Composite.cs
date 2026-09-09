#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static partial class McpToolResults
{
    /// </summary>
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

        var originalText = ReadText(result);
        var truncatedSections = new SortedSet<string>(StringComparer.Ordinal);
        var rootTruncated = false;
        var textLimit = CompositeWireBudgetBytes / 2;

        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;

            while (MeasureSectionNode(section, string.Equals(sectionName, rootSectionName, StringComparison.Ordinal)) > CompositeSectionBudgetBytes)
            {
                if (TryTrimSectionOnce(section))
                {
                    MarkSectionTruncated(section, sectionName);
                    truncatedSections.Add(sectionName);
                    continue;
                }

                if (rootSectionName != sectionName)
                {
                    payload[sectionName] = CreateTruncatedSection(sectionName);
                    MarkSectionTruncated((JsonObject)payload[sectionName]!, sectionName);
                    truncatedSections.Add(sectionName);
                }

                break;
            }
        }

        if (truncatedSections.Count > 0)
        {
            MarkRootTruncated(payload, truncatedSections);
            rootTruncated = true;
        }

        var projectedText = originalText;
        for (var attempt = 0; attempt < 4096; attempt++)
        {
            var hint = BuildCompositeBudgetHint(truncatedSections, rootTruncated);
            projectedText = truncatedSections.Count > 0 || rootTruncated
                ? BuildBudgetSafeText(payload, sectionNames, rootSectionName, hint, textLimit)
                : originalText;

            var candidate = ReplaceText(
                ReplaceStructured(result, JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default)),
                projectedText);
            candidate = UpdateCompositeWireBudget(
                candidate,
                payload,
                sectionNames,
                rootSectionName,
                truncatedSections.Count > 0 || rootTruncated);

            var measurement = MeasureComposite(candidate);
            if (measurement.TotalBytes <= CompositeWireBudgetBytes
                && SectionsFit(payload, sectionNames, rootSectionName))
            {
                return candidate;
            }

            if (TryTrimLargestSection(payload, sectionNames, rootSectionName, truncatedSections))
            {
                MarkRootTruncated(payload, truncatedSections);
                rootTruncated = true;
                continue;
            }

            var textBudget = Math.Max(1, CompositeWireBudgetBytes - measurement.StructuredBytes);
            textLimit = Math.Min(textLimit, textBudget);
            rootTruncated = true;
            MarkRootTruncated(payload, truncatedSections);
            var nextText = BuildBudgetSafeText(
                payload,
                sectionNames,
                rootSectionName,
                BuildCompositeBudgetHint(truncatedSections, true),
                textLimit);
            if (nextText == projectedText && measurement.TotalBytes > CompositeWireBudgetBytes)
            {
                return candidate;
            }
        }

        return UpdateCompositeWireBudget(
            ReplaceText(
                ReplaceStructured(result, JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default)),
                projectedText),
            payload,
            sectionNames,
            rootSectionName,
            truncatedSections.Count > 0 || rootTruncated);
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
            stringCandidate.Parent[stringCandidate.Key] = TrimUtf8(
                stringCandidate.Value,
                Math.Max(1, currentBytes / 2));
            return true;
        }

        return false;
    }

}
