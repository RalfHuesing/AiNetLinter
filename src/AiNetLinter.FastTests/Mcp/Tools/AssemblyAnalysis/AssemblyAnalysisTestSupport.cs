#nullable enable

using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisTestSupport
{
    internal static T Deserialize<T>(CallToolResult result)
    {
        Assert.True(result.StructuredContent.HasValue, TextOf(result));
        return JsonSerializer.Deserialize<T>(result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
    }

    internal static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
