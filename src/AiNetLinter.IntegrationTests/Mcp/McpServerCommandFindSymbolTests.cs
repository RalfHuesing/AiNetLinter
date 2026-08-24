using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;


[Trait("Category", "Integration")]
public sealed class McpServerCommandFindSymbolTests
{
    private readonly ReadOnlyMcpHostFixture fixture;

    public McpServerCommandFindSymbolTests(ReadOnlyMcpHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates()
    {
        var host = await fixture.GetHostAsync();
        var text = await host.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "Greet" }, ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_BatchCallWithMatchAndMiss_ReturnsSectionsAndMissHint()
    {
        var host = await fixture.GetHostAsync();
        var text = await host.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "Greeter", "NonExistentFooBar" } });

        Assert.Contains("Symbol-Suche: `Greeter`", text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.Contains("Symbol-Suche: `NonExistentFooBar`", text, StringComparison.Ordinal);
        Assert.Contains("Keine Treffer fuer 'NonExistentFooBar'", text, StringComparison.Ordinal);
    }
}
