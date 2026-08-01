using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Tests fuer <c>get_impact</c> ausgelagert aus <c>McpServerCommandTests.cs</c>, weil diese Datei
/// bereits am <c>MaxLineCount: 500</c>-Limit liegt. Thematisch fokussiert auf die 005-Erweiterung:
/// P0/P1-Trunkierung via <c>maxResults</c>-Parameter (Konzept Z. 215-225) im realen MCP-Subprozess.
/// Zwei Tests: Symbol-Branch (Caller.cs-Fixture) und Git-Branch (CalculatorCaller.cs-Fixture).
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerCommandGetImpactTests
{
    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactSymbolBranchWithMaxResultsTruncates()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet", ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactGitBranchWithMaxResultsTruncates()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();

        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?> { ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
