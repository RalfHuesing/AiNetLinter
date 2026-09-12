#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class SearchPatternToolTests
{
    private readonly SymbolGraphCatalogFixture fixture;

    public SearchPatternToolTests(SymbolGraphCatalogFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture()
    {
        using var state = fixture.CreateReadOnlyServer();
        var result = await SearchPatternTool.ExecuteAsync(state, "Greeter", false, 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRegex_ReturnsRecoverableInvalidArgument()
    {
        using var state = fixture.CreateReadOnlyServer();
        var result = await SearchPatternTool.ExecuteAsync(state, "(unclosed", true, 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EnrichCSharp_ReturnsSemanticObjectAndKeepsTextPayload()
    {
        using var state = fixture.CreateReadOnlyServer();
        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 0, null, null, null, true),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        var matches = result.StructuredContent!.Value.GetProperty("matches").EnumerateArray().ToArray();
        var declaration = Assert.Single(matches.Where(match =>
            match.GetProperty("semantic").GetProperty("kind").GetString() == "declaration"));
        Assert.Equal("resolved", declaration.GetProperty("semantic").GetProperty("resolution").GetString());
        Assert.Equal("T:SymbolGraphMini.Greeter", declaration.GetProperty("semantic").GetProperty("symbolId").GetString());
        Assert.Contains(matches, match =>
            match.GetProperty("semantic").GetProperty("kind").GetString() == "symbol_reference");
    }
}
