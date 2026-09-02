using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task RunAsync_AssemblyTarget_GetImpactUsesAssemblySessionAndOriginContract()
    {
        var result = await fixture.Client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["targetType"] = "assembly",
                ["targetPath"] = typeof(AiNetLinter.Mcp.McpCodeGraphServer).Assembly.Location,
                ["symbolIdentifier"] = "McpCodeGraphServer.GetCurrentSolution",
                ["maxResults"] = 10,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var analysis = result.StructuredContent!.Value.GetProperty("analysis");
        Assert.Equal("assembly", analysis.GetProperty("targetType").GetString());
        Assert.Equal("decompiled", analysis.GetProperty("origin").GetString());
        Assert.DoesNotContain(
            "ASSEMBLY_TARGET_UNSUPPORTED",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AssemblyTarget_GetImpactWithoutSymbolReturnsRecoverableInvalidArgument()
    {
        var result = await fixture.Client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["targetType"] = "assembly",
                ["targetPath"] = typeof(AiNetLinter.Mcp.McpCodeGraphServer).Assembly.Location,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Contains(
            "symbolIdentifier",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text,
            StringComparison.Ordinal);
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
