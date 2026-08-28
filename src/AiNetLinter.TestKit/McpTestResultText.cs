#nullable enable

using ModelContextProtocol.Protocol;

namespace AiNetLinter.TestKit;

public static class McpTestResultText
{
    public static string TextOf(CallToolResult result) =>
        result.Content is { Count: > 0 } && result.Content[0] is TextContentBlock text
            ? text.Text ?? string.Empty
            : string.Empty;
}
