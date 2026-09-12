#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static partial class McpToolResults
{
    private static List<ContentBlock> AppendNavigationText(
        IEnumerable<ContentBlock> content,
        string navigationText)
    {
        var body = string.Join("\n", content.OfType<TextContentBlock>().Select(block => block.Text)).TrimEnd();
        var evidence = new List<AgentContentEvidence>();
        if (!string.IsNullOrWhiteSpace(body)) evidence.Add(new AgentContentEvidence(body, IsRequired: true));
        if (!string.IsNullOrWhiteSpace(navigationText)
            && !body.Contains(navigationText, StringComparison.Ordinal))
        {
            evidence.Add(new AgentContentEvidence(navigationText, IsRequired: true));
        }

        var rendered = ContentRenderer.Render(new AgentContentRenderRequest(
            IsError: false,
            Evidence: evidence));
        return [new TextContentBlock { Text = rendered.Text }];
    }
}
