#nullable enable

using ModelContextProtocol.Protocol;
using System.Text.RegularExpressions;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisTestSupport
{
    private static readonly Regex ContinuationTokenPattern = new(
        "continuationToken: `(?<token>[^`]+)`",
        RegexOptions.CultureInvariant);

    internal static string TextOf(CallToolResult result)
    {
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(string.IsNullOrWhiteSpace(block.Text));
        return block.Text;
    }

    internal static string ContinuationTokenOf(CallToolResult result)
    {
        var match = ContinuationTokenPattern.Match(TextOf(result));
        Assert.True(match.Success, TextOf(result));
        return match.Groups["token"].Value;
    }
}
