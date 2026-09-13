#nullable enable

using System;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal static class McpHandoffTestHelper
{
    internal static string Extract(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var match = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException("Die Toolantwort muss ein opaques Handoff ausgeben.");
    }
}
