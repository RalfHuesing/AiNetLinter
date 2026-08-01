using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Tests fuer <c>find_symbol</c> ausgelagert aus <c>McpServerCommandTests.cs</c>, weil diese
/// Datei bereits am <c>MaxLineCount: 500</c>-Limit liegt (siehe 003-Result.md Beobachtung 3).
/// Thematisch fokussiert auf die 004-Erweiterungen: P0/P1-Trunkierung via <c>maxResults</c>-Parameter
/// (Konzept Z. 215-225) im realen MCP-Subprozess.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandFindSymbolTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "Greet", ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
