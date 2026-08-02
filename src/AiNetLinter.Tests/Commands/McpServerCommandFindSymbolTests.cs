using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

public sealed class McpServerCommandFindSymbolTests : IClassFixture<SymbolGraphMcpFixture>
{
    private readonly SymbolGraphMcpFixture _fixture;

    public McpServerCommandFindSymbolTests(SymbolGraphMcpFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "Greet", ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
