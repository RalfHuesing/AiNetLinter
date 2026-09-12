#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class GetClassStructureResponseBudget
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0 || McpResponseSize.From(result).TotalBytes <= maxResponseBytes) return result;
        return McpToolResults.InvalidArgument(
            "maxResponseBytes ist zu klein für die fachliche Klassenstruktur-Antwort.",
            "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Member-Einheiten gekürzt.",
            "$.maxResponseBytes");
    }

    private static bool TryReadBudgetParts(
        CallToolResult result,
        int maxResponseBytes,
        out BudgetParts parts)
    {
        parts = default;
        return false;
    }

    private static ClassStructurePayload TrimToBudget(BudgetParts parts, int maxResponseBytes)
    {
        var members = parts.Payload.Members.ToList();
        var truncatedBy = (parts.Payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        var candidate = parts.Payload;
        while (CombinedResponseBytes(
                   RenderBudgetText(candidate, parts.Text, ProjectEnvelope(parts.Envelope, candidate)),
                   ProjectEnvelope(parts.Envelope, candidate)) > maxResponseBytes
            && members.Count > 0)
        {
            members.RemoveAt(members.Count - 1);
            if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal)) truncatedBy.Add("maxResponseBytes");
            candidate = candidate with
            {
                Members = members.ToList(),
                ShownMemberCount = members.Count,
                Truncated = true,
                TruncatedBy = truncatedBy,
                Next = BudgetNext,
            };
        }
        return candidate;
    }

    private static CallToolResult CreateBudgetedResult(
        CallToolResult original,
        string text,
        JsonObject envelope) => new()
        {
            IsError = original.IsError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };

    private static JsonObject ProjectEnvelope(JsonObject original, ClassStructurePayload payload)
    {
        var projected = (JsonObject)original.DeepClone();
        var payloadNode = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject
            ?? new JsonObject();
        foreach (var name in ClassPayloadFields)
        {
            projected.Remove(name);
            if (payloadNode[name] is { } value) projected[name] = value.DeepClone();
        }

        // The source route adds navigation before this final, tool-specific budget pass.  If
        // that extra navigation overhead causes the member list to be trimmed here, the
        // navigation projection must describe the delivered subset too; otherwise it can still
        // claim a complete result and advise `none` even though the class structure is partial.
        if (payload.TruncatedBy?.Contains("maxResponseBytes", StringComparer.Ordinal) == true
            && projected["navigation"] is JsonObject navigation)
        {
            if (navigation["status"] is JsonObject status)
            {
                status["completeness"] = "truncated";
            }
            navigation["next"] = new JsonObject
            {
                ["kind"] = BudgetNext.Kind,
                ["action"] = BudgetNext.Reason,
            };
        }
        return projected;
    }

    private static readonly string[] ClassPayloadFields =
    [
        "typeName", "kind", "files", "totalLines", "totalMemberCount", "shownMemberCount",
        "truncated", "members", "truncatedBy", "next",
    ];

    internal static string RenderBudgetText(
        ClassStructurePayload payload,
        string fallbackText,
        JsonObject? finalEnvelope = null)
    {
        var rendered = GetClassStructureTool.RenderMarkdown(payload);
        var headingIndex = rendered.IndexOf("# Typ:", StringComparison.Ordinal);
        var originalHeading = fallbackText.IndexOf("# Typ:", StringComparison.Ordinal);
        if (originalHeading > 0 && headingIndex == 0)
        {
            rendered = fallbackText[..originalHeading].TrimEnd() + "\n\n" + rendered;
        }
        var navigationIndex = fallbackText.IndexOf("## Navigation", StringComparison.Ordinal);
        if (navigationIndex < 0)
        {
            return AppendRequiredNavigationText(rendered, finalEnvelope);
        }

        return rendered.TrimEnd() + "\n\n" + SynchronizeNavigationText(
                fallbackText[navigationIndex..].Trim(),
                finalEnvelope);
    }

    private static string AppendRequiredNavigationText(string rendered, JsonObject? envelope)
    {
        var navigation = envelope?["navigation"] as JsonObject;
        var status = navigation?["status"] as JsonObject;
        var completeness = status is null ? null : ReadString(status, "completeness");
        if (string.IsNullOrWhiteSpace(completeness) || completeness == "complete") return rendered;

        var operation = ReadString(status!, "operation") ?? "ok";
        var next = navigation?["next"] as JsonObject;
        var action = next is null ? null : ReadString(next, "action");
        var suffix = $"Status: operation={operation}, completeness={completeness}";
        return string.IsNullOrWhiteSpace(action)
            ? rendered + "\n\n" + suffix
            : rendered + "\n\n" + suffix + "; Aktion: " + action;
    }

    private static string SynchronizeNavigationText(string navigationText, JsonObject? envelope)
    {
        if (envelope?["navigation"] is not JsonObject navigation)
        {
            return navigationText;
        }

        var completeness = (navigation["status"] as JsonObject) is { } status
            ? ReadString(status, "completeness")
            : null;
        var next = navigation["next"] as JsonObject;
        var nextKind = next is null ? null : ReadString(next, "kind");
        var nextAction = next is null ? null : ReadString(next, "action");
        if (completeness is null && nextKind is null && nextAction is null)
        {
            return navigationText;
        }

        return string.Join("\n", navigationText.Split('\n')
            .Select(line => RewriteNavigationLine(line, completeness, nextKind, nextAction)));
    }

    private static string RewriteNavigationLine(string line, string? completeness, string? nextKind, string? nextAction)
    {
        var value = line.TrimEnd('\r');
        if (completeness is not null && value.StartsWith("- status: ", StringComparison.Ordinal))
            return $"- status: operation=`ok`, completeness=`{completeness}`";
        if (completeness is not null && value.StartsWith("- completeness: ", StringComparison.Ordinal))
            return $"- completeness: `{completeness}`";
        if (nextKind is not null && value.StartsWith("- next: ", StringComparison.Ordinal))
            return $"- next: `{nextKind}` — {nextAction ?? string.Empty}";
        return value;
    }

    private static string? ReadString(JsonObject owner, string propertyName) =>
        owner[propertyName] is JsonValue value
        && value.TryGetValue<string>(out var text)
            ? text
            : null;

    internal static int CombinedResponseBytes(string text, ClassStructurePayload payload) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    internal static int CombinedResponseBytes(string text, JsonObject envelope) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static readonly ClassStructureNext BudgetNext =
        new("request_detail", "maxResponseBytes erhöhen oder symbolIdentifier/kindFilter/nameFilter verfeinern.");

    private readonly record struct BudgetParts(
        ClassStructurePayload Payload,
        string Text,
        JsonObject Envelope);
}
