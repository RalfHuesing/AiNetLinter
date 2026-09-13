#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class Group1CoreHandoffTests
{
    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    [Fact]
    public async Task FindSymbol_EmitsOpaqueHandoffHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(
            fixture.CreateServer(),
            namePatterns: ["Greeter"],
            kind: "class",
            maxResults: 10,
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("handoffId: `h:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_To_GetSymbolBody_Roundtrip_SucceedsWithOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var findResult = await FindSymbolTool.ExecuteAsync(
            fixture.CreateServer(),
            namePatterns: ["Greeter"],
            kind: "class",
            maxResults: 10,
            CancellationToken.None);

        var text = TextOf(findResult);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`");
        Assert.True(match.Success, $"Expected h:... handle in: {text}");
        var handle = match.Groups["handle"].Value;

        var bodyResult = await GetSymbolBodyTool.ExecuteAsync(
            fixture.CreateServer(),
            [handle],
            80,
            CancellationToken.None);

        Assert.NotEqual(true, bodyResult.IsError);
        var bodyText = TextOf(bodyResult);
        Assert.Contains("class Greeter", bodyText, StringComparison.Ordinal);
        Assert.Contains($"handoffId: `{handle}`", bodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_To_FindReferences_Roundtrip_SucceedsWithOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var findResult = await FindSymbolTool.ExecuteAsync(
            fixture.CreateServer(),
            namePatterns: ["Greet"],
            kind: "method",
            maxResults: 10,
            CancellationToken.None);

        var text = TextOf(findResult);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`");
        Assert.True(match.Success, $"Expected h:... handle in: {text}");
        var handle = match.Groups["handle"].Value;

        var refResult = await FindReferencesTool.ExecuteAsync(
            fixture.CreateServer(),
            handle,
            50,
            1,
            CancellationToken.None);

        Assert.NotEqual(true, refResult.IsError);
        var refText = TextOf(refResult);
        Assert.Contains("Caller.cs", refText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSymbolBody_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var bodyResult = await GetSymbolBodyTool.ExecuteAsync(
            fixture.CreateServer(),
            ["h:zzzz999"],
            80,
            CancellationToken.None);

        var text = TextOf(bodyResult);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSymbolBody_WithInvalidHandle_ReturnsInvalidHandoff()
    {
        using var fixture = new McpInMemoryTestContext();
        var bodyResult = await GetSymbolBodyTool.ExecuteAsync(
            fixture.CreateServer(),
            ["h:??!!"],
            80,
            CancellationToken.None);

        var text = TextOf(bodyResult);
        Assert.Contains(LinterErrorCodes.InvalidHandoff, text, StringComparison.Ordinal);
    }
}
