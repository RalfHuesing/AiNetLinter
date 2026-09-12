#nullable enable

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

internal static partial class GetSymbolBodyTool
{
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0 || Mcp.Wire.McpResponseSize.From(result).TotalBytes <= maxResponseBytes)
        {
            return result;
        }

        if (result.StructuredContent is not { } structured)
        {
            return BudgetTooSmall(maxResponseBytes);
        }

        var payload = JsonSerializer.Deserialize<SymbolBodyBatchDto>(structured.GetRawText(), McpJsonOptions.Default);
        if (payload is null) return BudgetTooSmall(maxResponseBytes);

        var units = payload.Results.Select(entry => new SymbolBodyRenderUnit(entry, RenderEntryMarkdown(entry))).ToList();
        while (units.Count > 0)
        {
            var projected = CreateBudgetedResult(units, payload.RequestedCount, maxResponseBytes);
            if (projected.IsError == true) break;
            var root = JsonNode.Parse(projected.StructuredContent!.Value.GetRawText())!.AsObject();
            var originalRoot = JsonNode.Parse(structured.GetRawText())!.AsObject();
            if (originalRoot["navigation"] is { } navigation) root["navigation"] = navigation.DeepClone();
            var final = new CallToolResult
            {
                Content = projected.Content,
                StructuredContent = JsonSerializer.SerializeToElement(root, McpJsonOptions.Default),
            };
            if (Mcp.Wire.McpResponseSize.From(final).TotalBytes <= maxResponseBytes) return final;
            units.RemoveAt(units.Count - 1);
        }

        return BudgetTooSmall(maxResponseBytes);
    }

    private static string RenderEntryMarkdown(SymbolBodyEntry entry)
    {
        var markdown = new MarkdownBuilder();
        markdown.Heading(3, $"Symbol-Body — `{entry.FilePath}`");
        markdown.BlankLine();
        markdown.Line($"bodyAvailability: `{entry.BodyAvailability}`; contentMode: `{entry.ContentMode}`");
        if (entry.TotalBodyLines > 0)
        {
            markdown.Line($"Zeilen: {entry.DisplayedStartLine}-{entry.DisplayedEndLine} von {entry.TotalBodyLines}");
        }
        markdown.BlankLine();
        markdown.CodeBlock("csharp", entry.Body ?? "// Für dieses Symbol ist kein dekompilierbarer Body verfügbar.");
        return markdown.Build().TrimEnd();
    }
}
