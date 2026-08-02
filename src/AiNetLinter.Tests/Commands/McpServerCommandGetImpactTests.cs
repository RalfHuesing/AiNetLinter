using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Tests fuer <c>get_impact</c> ausgelagert aus <c>McpServerCommandTests.cs</c>.
/// </summary>
public sealed class McpServerCommandGetImpactTests : IClassFixture<SymbolGraphMcpFixture>
{
    private readonly SymbolGraphMcpFixture _fixture;

    public McpServerCommandGetImpactTests(SymbolGraphMcpFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactSymbolBranchWithMaxResultsTruncates()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
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
