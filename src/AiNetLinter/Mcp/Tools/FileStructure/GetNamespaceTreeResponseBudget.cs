#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Budgetprojektion fuer Text und StructuredContent von <c>get_namespace_tree</c>.
/// </summary>
internal static class GetNamespaceTreeResponseBudget
{
    internal static CallToolResult Apply(
        string originalText,
        NamespaceTreePayload payload,
        int maxResponseBytes)
    {
        var candidate = PrepareCandidate(payload);
        if (maxResponseBytes <= 0)
        {
            return McpToolResults.Text(RenderVisibleText(candidate, originalText), candidate);
        }

        if (maxResponseBytes < McpResponseBudgetLimits.MinimumStructuredBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} Bytes betragen, damit Namespace-Text und StructuredContent gemeinsam markiert gekürzt werden können.",
                "maxResponseBytes weglassen, 0 verwenden oder mindestens 512 setzen.",
                "$.maxResponseBytes");
        }

        var truncatedBy = (payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        candidate = FitToBudget(candidate, originalText, maxResponseBytes, truncatedBy);
        return CreateBudgetedResult(candidate, originalText, maxResponseBytes, truncatedBy);
    }

    internal static CallToolResult ApplyFinal(CallToolResult result, int maxResponseBytes)
    {
        if (!TryReadPayload(result, maxResponseBytes, out var payload, out var text, out var envelope))
        {
            return result;
        }

        return FitFinalResult(result, text!, payload!, envelope!, maxResponseBytes);
    }

    private static NamespaceTreePayload PrepareCandidate(NamespaceTreePayload payload)
    {
        var visibleCount = NamespaceTreeProjection.VisibleEntryCount(payload);
        if (payload.Namespaces is not { Count: > 0 }
            || payload.ShownCount >= visibleCount)
        {
            return payload;
        }

        if (!string.IsNullOrWhiteSpace(payload.NamespacePrefix)
            && payload.Namespaces is [var exactRoot])
        {
            var children = exactRoot.SubNamespaces is null
                ? (IReadOnlyList<NamespaceTreeNode>)[]
                : NamespaceTreeProjection.Take(exactRoot.SubNamespaces, payload.ShownCount).Nodes;
            return payload with
            {
                Namespaces = [exactRoot with { SubNamespaces = children.Count > 0 ? children : null }],
            };
        }

        return payload with
        {
            Namespaces = NamespaceTreeProjection.Take(payload.Namespaces, payload.ShownCount).Nodes,
        };
    }

    private static NamespaceTreePayload FitToBudget(
        NamespaceTreePayload candidate,
        string originalText,
        int maxResponseBytes,
        List<string> truncatedBy)
    {
        var initialVisibleCount = NamespaceTreeProjection.VisibleEntryCount(candidate);
        if (initialVisibleCount == 0 && !candidate.Truncated)
        {
            return candidate;
        }

        var minimum = MinimumProjection(candidate, initialVisibleCount, truncatedBy);
        var minimumVisibleCount = NamespaceTreeProjection.VisibleEntryCount(minimum);
        while (NamespaceTreeProjection.VisibleEntryCount(candidate) > minimumVisibleCount)
        {
            var budgetCandidate = MarkTruncated(candidate, truncatedBy);
            if (CombinedResponseBytes(RenderVisibleText(budgetCandidate, originalText), budgetCandidate) <= maxResponseBytes)
            {
                return NamespaceTreeProjection.VisibleEntryCount(candidate) == initialVisibleCount
                    ? candidate
                    : budgetCandidate;
            }

            candidate = RemoveLastVisibleEntry(candidate);
        }

        var minimumCandidate = initialVisibleCount > minimumVisibleCount
            ? MarkTruncated(candidate, truncatedBy)
            : candidate;
        return CombinedResponseBytes(RenderVisibleText(minimumCandidate, originalText), minimumCandidate) <= maxResponseBytes
            ? minimumCandidate
            : minimum;
    }

    private static CallToolResult CreateBudgetedResult(
        NamespaceTreePayload candidate,
        string originalText,
        int maxResponseBytes,
        List<string> truncatedBy)
    {
        var text = RenderVisibleText(candidate, originalText);
        return CombinedResponseBytes(text, candidate) > maxResponseBytes
            ? BudgetTooSmall(maxResponseBytes, candidate, originalText)
            : McpToolResults.Text(text, candidate);
    }

    private static NamespaceTreePayload MarkTruncated(
        NamespaceTreePayload candidate,
        List<string> truncatedBy)
    {
        if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal))
        {
            truncatedBy.Add("maxResponseBytes");
        }

        return candidate with
        {
            ShownCount = NamespaceTreeProjection.VisibleEntryCount(candidate),
            Truncated = true,
            TruncatedBy = truncatedBy,
            Next = BudgetNext(),
        };
    }

    private static NamespaceTreeNext BudgetNext() =>
        new("request_detail", "maxResponseBytes erhöhen oder project/namespacePrefix/maxResults verfeinern.");

    private static bool TryReadPayload(
        CallToolResult result,
        int maxResponseBytes,
        out NamespaceTreePayload? payload,
        out string? text,
        out JsonObject? envelope)
    {
        payload = null;
        text = null;
        envelope = null;
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || maxResponseBytes <= 0
            || !HasNamespacePayload(structured))
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<NamespaceTreePayload>(structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return false;
        }

        text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        envelope = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        return payload is not null && text is not null && envelope is not null;
    }

    private static bool HasNamespacePayload(JsonElement structured) =>
        structured.TryGetProperty("projects", out _)
        || structured.TryGetProperty("namespaces", out _)
        || structured.TryGetProperty("types", out _);

    private static CallToolResult FitFinalResult(
        CallToolResult original,
        string originalText,
        NamespaceTreePayload payload,
        JsonObject envelope,
        int maxResponseBytes)
    {
        if (CombinedResponseBytes(originalText, envelope) <= maxResponseBytes)
        {
            return original;
        }

        var truncatedBy = (payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        var candidate = payload;
        var initialVisibleCount = NamespaceTreeProjection.VisibleEntryCount(candidate);
        if (initialVisibleCount == 0 && !candidate.Truncated)
        {
            return original;
        }

        var minimum = MinimumProjection(candidate, initialVisibleCount, truncatedBy);
        var minimumVisibleCount = NamespaceTreeProjection.VisibleEntryCount(minimum);
        while (NamespaceTreeProjection.VisibleEntryCount(candidate) > minimumVisibleCount)
        {
            var hasVisibleReduction =
                NamespaceTreeProjection.VisibleEntryCount(candidate) < initialVisibleCount;
            var budgetCandidate = hasVisibleReduction
                ? MarkTruncated(candidate, truncatedBy)
                : candidate;
            var projected = ProjectEnvelope(envelope, budgetCandidate);
            var rendered = RenderVisibleText(budgetCandidate, originalText);
            if (CombinedResponseBytes(rendered, projected) <= maxResponseBytes)
            {
                return new CallToolResult
                {
                    IsError = original.IsError,
                    Content = new List<ContentBlock> { new TextContentBlock { Text = rendered } },
                    StructuredContent = JsonSerializer.SerializeToElement(projected, McpJsonOptions.Default),
                };
            }

            candidate = RemoveLastVisibleEntry(candidate);
        }

        var minimumCandidate = initialVisibleCount > minimumVisibleCount
            ? MarkTruncated(candidate, truncatedBy)
            : candidate;
        var minimumEnvelope = ProjectEnvelope(envelope, minimumCandidate);
        var minimumText = RenderVisibleText(minimumCandidate, originalText);
        if (CombinedResponseBytes(minimumText, minimumEnvelope) <= maxResponseBytes)
        {
            return new CallToolResult
            {
                IsError = original.IsError,
                Content = new List<ContentBlock> { new TextContentBlock { Text = minimumText } },
                StructuredContent = JsonSerializer.SerializeToElement(minimumEnvelope, McpJsonOptions.Default),
            };
        }

        return BudgetTooSmall(maxResponseBytes, minimumCandidate, minimumText, minimumEnvelope);
    }

    private static NamespaceTreePayload MinimumProjection(
        NamespaceTreePayload payload,
        int initialVisibleCount,
        List<string> truncatedBy)
    {
        if (initialVisibleCount == 0) return payload;

        var minimum = payload.Projects is { Count: > 0 } projects
            ? payload with { Projects = projects.Take(1).ToList() }
            : payload.Types is { Count: > 0 } types
                ? payload with
                {
                    Types = types.Take(1).ToList(),
                    Namespaces = ProjectTypeNamespace(payload.Namespaces, types.Take(1).ToList()),
                }
                : MinimumNamespaceProjection(payload);

        return NamespaceTreeProjection.VisibleEntryCount(minimum) < initialVisibleCount
            ? MarkTruncated(minimum, truncatedBy)
            : minimum;
    }

    private static NamespaceTreePayload MinimumNamespaceProjection(NamespaceTreePayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.NamespacePrefix)
            && payload.Namespaces is [var exactRoot])
        {
            var children = exactRoot.SubNamespaces is { Count: > 0 }
                ? NamespaceTreeProjection.Take(exactRoot.SubNamespaces, 1).Nodes
                : null;
            return payload with
            {
                Namespaces = [exactRoot with { SubNamespaces = children }],
            };
        }

        return payload with
        {
            Namespaces = payload.Namespaces is { Count: > 0 } namespaces
                ? NamespaceTreeProjection.Take(namespaces, 1).Nodes
                : null,
        };
    }

    private static CallToolResult BudgetTooSmall(
        int requestedBytes,
        NamespaceTreePayload minimum,
        string originalText,
        JsonObject? envelope = null)
    {
        var minimumBytes = envelope is null
            ? CombinedResponseBytes(RenderVisibleText(minimum, originalText), minimum)
            : CombinedResponseBytes(originalText, envelope);
        return McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={requestedBytes} ist zu klein für die fachliche Namespace-Mindestprojektion; Mindestwert: {minimumBytes} Bytes.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimumBytes} setzen; die Antwort wird nur an vollständigen Namespace-/Typ-Einheiten gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: requestedBytes,
                MinimumResponseBytes: minimumBytes));
    }

    private static JsonObject ProjectEnvelope(JsonObject original, NamespaceTreePayload payload)
    {
        var projected = (JsonObject)original.DeepClone();
        var payloadNode = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject
            ?? new JsonObject();
        foreach (var name in NamespacePayloadFields)
        {
            projected.Remove(name);
            if (payloadNode[name] is { } value) projected[name] = value.DeepClone();
        }

        return projected;
    }

    private static readonly string[] NamespacePayloadFields =
    [
        "solutionName", "project", "namespacePrefix", "kindFilter", "depth", "includeTypes",
        "totalCount", "shownCount", "truncated", "projects", "namespaces", "types",
        "requestedDepth", "effectiveDepth", "depthWasClamped", "truncatedBy", "next",
    ];

    private static int CombinedResponseBytes(string text, NamespaceTreePayload payload) =>
        Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    private static int CombinedResponseBytes(string text, JsonObject envelope) =>
        Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static bool HasVisibleEntries(NamespaceTreePayload payload) =>
        NamespaceTreeProjection.VisibleEntryCount(payload) > 0;

    private static NamespaceTreePayload RemoveLastVisibleEntry(NamespaceTreePayload payload)
    {
        if (payload.Projects is { Count: > 0 } projects)
        {
            return payload with { Projects = projects.Take(projects.Count - 1).ToList() };
        }
        if (payload.Types is { Count: > 0 } types)
        {
            var remainingTypes = types.Take(types.Count - 1).ToList();
            return payload with
            {
                Types = remainingTypes,
                Namespaces = ProjectTypeNamespace(payload.Namespaces, remainingTypes),
            };
        }
        if (payload.Namespaces is { Count: > 0 } namespaces)
        {
            if (!string.IsNullOrWhiteSpace(payload.NamespacePrefix) && namespaces is [var exactRoot])
            {
                var children = exactRoot.SubNamespaces;
                return payload with
                {
                    Namespaces =
                    [
                        exactRoot with
                        {
                            SubNamespaces = children is { Count: > 0 }
                                ? NamespaceTreeProjection.RemoveLast(children, children.Sum(NamespaceTreeProjection.CountEntries))
                                : null,
                        },
                    ],
                };
            }

            return payload with
            {
                Namespaces = NamespaceTreeProjection.RemoveLast(
                    namespaces,
                    NamespaceTreeProjection.VisibleEntryCount(payload)),
            };
        }

        return payload;
    }

    private static IReadOnlyList<NamespaceTreeNode>? ProjectTypeNamespace(
        IReadOnlyList<NamespaceTreeNode>? namespaces,
        IReadOnlyList<TypeNodeEntry> types)
    {
        if (namespaces is not { Count: > 0 }) return namespaces;

        return namespaces
            .Select((node, index) => index == 0 ? node with { Types = types } : node)
            .ToList();
    }

    private static string RenderVisibleText(NamespaceTreePayload payload, string fallbackText)
    {
        var sb = new StringBuilder();
        AppendPayloadText(sb, payload);
        AppendTruncationFooter(sb, payload);
        return RestoreEnvelopeText(sb.ToString().TrimEnd(), fallbackText);
    }

    private static void AppendPayloadText(StringBuilder sb, NamespaceTreePayload payload)
    {
        if (payload.Projects is not null)
        {
            AppendProjectText(sb, payload);
            return;
        }
        if (payload.Types is not null)
        {
            AppendTypeText(sb, payload);
            return;
        }

        AppendNamespaceTreeText(sb, payload);
    }

    private static void AppendProjectText(StringBuilder sb, NamespaceTreePayload payload)
    {
        sb.AppendLine($"# Solution Overview: {payload.SolutionName} ({payload.TotalCount} Projekte)\n");
        foreach (var project in payload.Projects!)
        {
            sb.AppendLine($"- {project.ProjectName} (Typ: {project.ProjectType}, {project.NamespaceCount} Namespaces, {project.TypeCount} Typen)");
        }
    }

    private static void AppendTypeText(StringBuilder sb, NamespaceTreePayload payload)
    {
        sb.AppendLine($"# Typen in Namespace '{payload.NamespacePrefix}' (Projekt: {payload.Project}):\n");
        foreach (var type in payload.Types!)
        {
            sb.AppendLine($"- {type.Name} ({type.Kind}) — {type.FilePath}:{type.Line}");
        }
    }

    private static void AppendNamespaceTreeText(StringBuilder sb, NamespaceTreePayload payload)
    {
        var prefix = string.IsNullOrWhiteSpace(payload.NamespacePrefix) ? string.Empty : $" unter '{payload.NamespacePrefix}'";
        sb.AppendLine($"# Namespaces in Projekt '{payload.Project}'{prefix}:\n");
        foreach (var node in payload.Namespaces ?? Array.Empty<NamespaceTreeNode>())
        {
            AppendNamespaceText(sb, node, 0);
        }
    }

    private static void AppendTruncationFooter(StringBuilder sb, NamespaceTreePayload payload)
    {
        if (!payload.Truncated) return;

        sb.AppendLine();
        sb.Append($"[{payload.TotalCount} Eintraege gesamt, {payload.ShownCount} gezeigt — maxResponseBytes/maxResults erhoehen]");
    }

    private static string RestoreEnvelopeText(string rendered, string fallbackText)
    {
        var headingIndex = rendered.IndexOf('#');
        var originalHeading = fallbackText.IndexOf('#');
        if (originalHeading > 0 && headingIndex == 0)
        {
            rendered = RestoreHeading(rendered, fallbackText, originalHeading);
        }

        var navigationIndex = fallbackText.IndexOf("## Navigation", StringComparison.Ordinal);
        return navigationIndex >= 0
            ? rendered + "\n\n" + fallbackText[navigationIndex..].Trim()
            : rendered;
    }

    private static string RestoreHeading(string rendered, string fallbackText, int originalHeading)
    {
        var originalHeadingEnd = fallbackText.IndexOf('\n', originalHeading);
        var originalHeadingLine = originalHeadingEnd < 0
            ? fallbackText[originalHeading..].TrimEnd('\r')
            : fallbackText[originalHeading..originalHeadingEnd].TrimEnd('\r');
        if (originalHeadingLine.StartsWith("# Assembly Overview:", StringComparison.Ordinal))
        {
            var renderedLineEnd = rendered.IndexOf('\n');
            return fallbackText[..originalHeading].TrimEnd() + "\n\n" + originalHeadingLine
                + (renderedLineEnd < 0 ? string.Empty : rendered[renderedLineEnd..]);
        }

        return fallbackText[..originalHeading].TrimEnd() + "\n\n" + rendered;
    }

    private static void AppendNamespaceText(StringBuilder sb, NamespaceTreeNode node, int indent)
    {
        sb.AppendLine($"{new string(' ', indent * 2)}- {node.Namespace} ({node.TypeCount} Typen)");
        foreach (var child in node.SubNamespaces ?? Array.Empty<NamespaceTreeNode>())
        {
            AppendNamespaceText(sb, child, indent + 1);
        }
    }
}
