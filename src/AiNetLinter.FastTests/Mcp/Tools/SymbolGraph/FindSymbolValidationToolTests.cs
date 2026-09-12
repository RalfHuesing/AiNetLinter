#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class FindSymbolValidationToolTests
{
    [Fact]
    public async Task ExecuteAsync_BothPatternFormsProvided_ReturnsRecoverableInvalidArgument()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(
            new FindSymbolRequest(
                fixture.CreateServer(),
                NamePatterns: ["Greeter"],
                Kind: null,
                MaxResults: 50,
                CancellationToken: CancellationToken.None,
                Pattern: "Caller"));

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("namePatterns", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("pattern", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPattern_ReturnsFieldAwareInvalidArgument()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(
            new FindSymbolRequest(
                fixture.CreateServer(),
                NamePatterns: null,
                Kind: null,
                MaxResults: 50,
                CancellationToken: CancellationToken.None,
                Pattern: " "));

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("fieldPath: $.pattern", textContent.Text, StringComparison.Ordinal);
    }
}
