#nullable enable

using System.Linq;
using System.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Wire;

/// <summary>Misst das finale, agentisch sichtbare UTF-8-Contentbudget einer MCP-Antwort.</summary>
internal readonly record struct McpResponseSize(int TextBytes)
{
    /// <summary>
    /// Das Responsebudget gilt dem tatsächlich sichtbaren Text. Fachliche Zwischendaten dürfen
    /// bis zum Hard Cut intern strukturiert vorliegen, entscheiden aber weder Auswahl noch Fit.
    /// </summary>
    internal int TotalBytes => TextBytes;

    internal static McpResponseSize From(CallToolResult result)
    {
        var textBytes = result.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        return new(textBytes);
    }
}
