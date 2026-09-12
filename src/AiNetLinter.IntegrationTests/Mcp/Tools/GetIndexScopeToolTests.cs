#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class GetIndexScopeToolTests
{
    private readonly SymbolGraphCatalogFixture fixture;

    public GetIndexScopeToolTests(SymbolGraphCatalogFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("SOLUTION_NOT_LOADED", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReturnsRealCatalogBreakdown()
    {
        using var state = fixture.CreateReadOnlyServer();
        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(".cs:", TextOf(result), StringComparison.Ordinal);
        Assert.Contains("Population:", TextOf(result), StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("breakdown").ValueKind);
        Assert.Equal("find_symbol", payload.GetProperty("routing").GetProperty("cSharp").GetProperty("tool").GetString());
        Assert.Equal("search_pattern", payload.GetProperty("routing").GetProperty("nonCSharp").GetProperty("tool").GetString());
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
