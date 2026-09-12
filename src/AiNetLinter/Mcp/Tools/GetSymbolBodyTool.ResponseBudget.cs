#nullable enable

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

        return BudgetTooSmall(maxResponseBytes);
    }

    private static string RenderEntryMarkdown(SymbolBodyEntry entry)
    {
        var markdown = new MarkdownBuilder();
        markdown.Heading(3, $"Symbol-Body — `{entry.FilePath}`");
        if (!string.IsNullOrWhiteSpace(entry.Id)) markdown.Line($"handoffId: `{entry.Id}`");
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
