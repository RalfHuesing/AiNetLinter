using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
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
    public async Task RunAsync_AssemblyTarget_GetImpactUsesAssemblySessionAndOriginContract()
    {
        var result = await fixture.Client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(AiNetLinter.Mcp.Server.McpCodeGraphServer).Assembly.Location,
                ["symbolIdentifier"] = "McpCodeGraphServer.GetCurrentSolution",
                ["maxResults"] = 10,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var analysis = result.StructuredContent!.Value.GetProperty("analysis");
        Assert.Equal("decompiled", analysis.GetProperty("origin").GetString());
        Assert.Equal("decompiled", analysis.GetProperty("origin").GetString());
        Assert.DoesNotContain(
            "ASSEMBLY_TARGET_UNSUPPORTED",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AssemblyTarget_GetImpactWithoutSymbolReturnsErrorInvalidArgument()
    {
        var result = await fixture.Client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(AiNetLinter.Mcp.Server.McpCodeGraphServer).Assembly.Location,
            });

        Assert.True(result.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Contains(
            "symbolIdentifier",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Single().Text,
            StringComparison.Ordinal);
    }
}
