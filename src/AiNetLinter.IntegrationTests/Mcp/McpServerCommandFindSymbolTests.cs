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
            new Dictionary<string, object?> { ["namePattern"] = "Greet", ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
