#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.Common;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static partial class GetClassStructureTool
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (!TryReadBudgetParts(result, maxResponseBytes, out var parts)) return result;

        var candidate = TrimToBudget(parts, maxResponseBytes);
        var finalEnvelope = ProjectEnvelope(parts.Envelope, candidate);
        var finalText = RenderBudgetText(candidate, parts.Text, finalEnvelope);
        if (CombinedResponseBytes(finalText, finalEnvelope) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Navigation-/Trunkierungs-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Member-Einheiten gekürzt.",
                "$.maxResponseBytes");
        }
        return CreateBudgetedResult(result, finalText, finalEnvelope);
    }

    private static bool TryReadBudgetParts(
        CallToolResult result,
        int maxResponseBytes,
        out BudgetParts parts)
    {
        parts = default;
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || maxResponseBytes <= 0)
        {
            return false;
        }

        ClassStructurePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ClassStructurePayload>(
                structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return false;
        }

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        var envelope = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload?.Members is null || payload.Files is null || text is null || envelope is null) return false;
        parts = new BudgetParts(payload, text, envelope);
        return true;
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
            StructuredContent = JsonSerializer.SerializeToElement(envelope, McpJsonOptions.Default),
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

    private static string RenderBudgetText(
        ClassStructurePayload payload,
        string fallbackText,
        JsonObject? finalEnvelope = null)
    {
        var rendered = payload.Truncated
            ? RenderMarkdown(payload)
            : RenderMarkdown(payload);
        var headingIndex = rendered.IndexOf("# Typ:", StringComparison.Ordinal);
        var originalHeading = fallbackText.IndexOf("# Typ:", StringComparison.Ordinal);
        if (originalHeading > 0 && headingIndex == 0)
        {
            rendered = fallbackText[..originalHeading].TrimEnd() + "\n\n" + rendered;
        }
        var navigationIndex = fallbackText.IndexOf("## Navigation", StringComparison.Ordinal);
        return navigationIndex < 0
            ? rendered
            : rendered.TrimEnd() + "\n\n" + SynchronizeNavigationText(
                fallbackText[navigationIndex..].Trim(),
                finalEnvelope);
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

    private static int CombinedResponseBytes(string text, ClassStructurePayload payload) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    private static int CombinedResponseBytes(string text, JsonObject envelope) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static readonly ClassStructureNext BudgetNext =
        new("request_detail", "maxResponseBytes erhöhen oder symbolIdentifier/kindFilter/nameFilter verfeinern.");

    private readonly record struct BudgetParts(
        ClassStructurePayload Payload,
        string Text,
        JsonObject Envelope);
}
