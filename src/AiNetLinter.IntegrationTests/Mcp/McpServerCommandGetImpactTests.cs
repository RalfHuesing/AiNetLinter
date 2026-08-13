using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// E2E-Tests fuer <c>get_impact</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerCommandGetImpactTests
{
    private readonly ReadOnlyMcpHostFixture fixture;

    public McpServerCommandGetImpactTests(ReadOnlyMcpHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactSymbolBranchWithMaxResultsTruncates()
    {
        var host = await fixture.GetHostAsync();
        var text = await host.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet", ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactGitBranchWithMaxResultsTruncates()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        workspace.ChangeCalculatorAddBodyWithoutCommitting();

        await using var client = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));

        var text = await client.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?> { ["maxResults"] = 2 });

        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
