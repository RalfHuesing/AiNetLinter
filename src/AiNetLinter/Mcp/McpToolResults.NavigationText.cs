#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static partial class McpToolResults
{
    private static List<ContentBlock> AppendNavigationText(
        IEnumerable<ContentBlock> content,
        string navigationText) => content
            .Select(block => block is TextContentBlock textBlock
                ? new TextContentBlock
                {
                    Text = string.IsNullOrEmpty(navigationText)
                        ? textBlock.Text.TrimEnd()
                        : string.IsNullOrWhiteSpace(textBlock.Text)
                            ? navigationText
                            : textBlock.Text.Contains(navigationText, StringComparison.Ordinal)
                                ? textBlock.Text.TrimEnd()
                                : textBlock.Text.TrimEnd() + "\n" + navigationText,
                }
                : block)
            .ToList();
}
