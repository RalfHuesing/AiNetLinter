#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class FindMagicValuesToolTests
{
    private readonly SymbolGraphCatalogFixture fixture;

    public FindMagicValuesToolTests(SymbolGraphCatalogFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsIsErrorTrueWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var result = await FindMagicValuesTool.ExecuteAsync(state, DefaultArgs(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("SOLUTION_NOT_LOADED", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReturnsStructuredCandidatePayload()
    {
        using var state = fixture.CreateReadOnlyServer();
        var result = await FindMagicValuesTool.ExecuteAsync(state, DefaultArgs(), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, payload.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, payload.GetProperty("magicValues").ValueKind);
        Assert.Equal("candidate", payload.GetProperty("resultType").GetString());
    }

    private static FindMagicValuesToolArgs DefaultArgs() => new(
        null, "all", "all", 1, FindMagicValuesScanner.DefaultMaxResults, null, false, false, false);

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
