#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>Projects internal assembly-session data onto the stable agent contract.</summary>
internal static partial class AssemblyPublicContract
{
    private static readonly HashSet<string> ForbiddenProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "cursor", "isTruncated", "generation", "currentGeneration", "lastGoodGeneration",
        "generatedPath", "generatedDocumentPath", "decompiledProjectDirectory",
        "decompiledProjectPath", "decompiledSourceRoot", "sourceProjectPath",
        "workspacePath", "workspaceDirectory", "cachePath", "cacheDirectory", "cacheRoot",
        "materializedPath", "materializationPath", "materialisatPath",
        "directoriesContinuationToken", "directoryContinuationToken",
    };

    private static readonly HashSet<string> DiagnosticProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "diagnostic", "diagnostics", "diagnosticSamples", "samples", "errorCause", "lastLoadError",
    };

    [GeneratedRegex("(?i)(?:[A-Z]:[\\\\/]|\\\\\\\\|/)[^\\s,;\\)\\]}`]+")]
    private static partial Regex AbsolutePathRegex();

    [GeneratedRegex("(?i)(?<=— `)(?:[A-Z]:[\\\\/]|\\\\\\\\|/)[^`]+(?=`)")]
    private static partial Regex BodyLocationPathRegex();

    internal static CallToolResult Project(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var structured = result.StructuredContent is { ValueKind: JsonValueKind.Object } value
            ? JsonSerializer.SerializeToElement(
                SanitizeNode(JsonNode.Parse(value.GetRawText()) ?? new JsonObject(), false),
                McpJsonOptions.Default)
            : result.StructuredContent;
        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? new TextContentBlock { Text = SanitizeText(text.Text) }
                : block)
            .ToList();

        var projected = new CallToolResult
        {
            IsError = result.IsError,
            Content = content,
            StructuredContent = structured,
        };
        return RefreshWireBudget(projected);
    }

    private static JsonNode SanitizeNode(JsonNode node, bool diagnosticContext)
    {
        return node switch
        {
            JsonObject obj => SanitizeObject(obj, diagnosticContext),
            JsonArray array => SanitizeArray(array, diagnosticContext),
            JsonValue value => SanitizeValue(value, diagnosticContext),
            _ => node,
        };
    }

    private static JsonObject SanitizeObject(JsonObject obj, bool diagnosticContext)
    {
        foreach (var property in obj.ToList())
        {
            if (ForbiddenProperties.Contains(property.Key))
            {
                obj.Remove(property.Key);
                continue;
            }

            if (property.Value is null) continue;
            var childDiagnosticContext = diagnosticContext || IsDiagnosticProperty(property.Key);
            var sanitized = SanitizeNode(property.Value, childDiagnosticContext);
            if (!ReferenceEquals(sanitized, property.Value)) obj[property.Key] = sanitized;
        }

        return obj;
    }

    private static JsonArray SanitizeArray(JsonArray array, bool diagnosticContext)
    {
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is null) continue;
            var sanitized = SanitizeNode(array[index]!, diagnosticContext);
            if (!ReferenceEquals(sanitized, array[index])) array[index] = sanitized;
        }

        return array;
    }

    private static JsonNode SanitizeValue(JsonValue value, bool diagnosticContext)
    {
        return diagnosticContext && value.TryGetValue<string>(out var text)
            ? JsonValue.Create(SanitizeDiagnostic(text))!
            : value;
    }

    private static bool IsDiagnosticProperty(string propertyName) =>
        DiagnosticProperties.Contains(propertyName)
        || propertyName.Contains("diagnostic", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeText(string text)
    {
        var lines = text.Split('\n')
            .Where(line => !line.Contains("Pfade: decompiledProject", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.TrimStart().StartsWith("- Generation:", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.TrimStart().StartsWith("- GeneratedPath:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line
                .Replace("generatedPath=", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("generation=", string.Empty, StringComparison.OrdinalIgnoreCase));
        return BodyLocationPathRegex().Replace(string.Join('\n', lines), "<decompiled-source>");
    }

    private static string SanitizeDiagnostic(string value) => AbsolutePathRegex().Replace(value, "<path>");

    private static CallToolResult RefreshWireBudget(CallToolResult result)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var node = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (node?["wireBudget"] is not JsonObject wireBudget
            || wireBudget["limitBytes"] is not JsonValue limitValue
            || !limitValue.TryGetValue<int>(out var limit))
        {
            return result;
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var structuredBytes = Encoding.UTF8.GetByteCount(node.ToJsonString(McpJsonOptions.Default));
            var textBytes = result.Content
                .OfType<TextContentBlock>()
                .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
            var totalBytes = structuredBytes + textBytes;
            wireBudget["limitBytes"] = limit;
            wireBudget["textBytes"] = textBytes;
            wireBudget["structuredBytes"] = structuredBytes;
            wireBudget["totalBytes"] = totalBytes;
            var next = JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
            if (next.GetRawText() == result.StructuredContent?.GetRawText())
            {
                return result;
            }

            result = new CallToolResult
            {
                IsError = result.IsError,
                Content = result.Content,
                StructuredContent = next,
            };
            node = JsonNode.Parse(next.GetRawText()) as JsonObject ?? node;
            wireBudget = node["wireBudget"] as JsonObject ?? wireBudget;
        }

        return result;
    }
}
