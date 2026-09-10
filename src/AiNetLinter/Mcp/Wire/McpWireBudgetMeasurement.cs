#nullable enable

using System.Linq;
using System.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Wire;

internal readonly record struct McpWireBudgetMeasurement(int TextBytes, int StructuredBytes)
{
    internal int TotalBytes => TextBytes + StructuredBytes;

    internal static McpWireBudgetMeasurement From(CallToolResult result)
    {
        var textBytes = result.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = result.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return new(textBytes, structuredBytes);
    }
}
