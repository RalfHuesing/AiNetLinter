#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class ResponseBudgetHandlerBoundaryTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(511, true)]
    [InlineData(512, false)]
    [InlineData(65_536, false)]
    [InlineData(65_537, true)]
    public async Task DirectHandlers_EnforcePublicResponseBudgetRange(int budget, bool invalid)
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var references = await FindReferencesTool.ExecuteAsync(
            server, new FindReferencesRequest("IProcessor", 50, 1, MaxResponseBytes: budget), CancellationToken.None);
        var hierarchy = await GetTypeHierarchyTool.ExecuteAsync(new GetTypeHierarchyRequest(
            server, "IProcessor", 50, McpScopeType.All, false, CancellationToken.None, budget));
        var implementations = await FindImplementationsTool.ExecuteAsync(new FindImplementationsRequest(
            server, "IProcessor", 50, McpScopeType.All, false, CancellationToken.None, budget));
        var bodies = await GetSymbolBodyTool.ExecuteAsync(
            server, new GetSymbolBodyRequest(["IProcessor.Execute"], MaxResponseBytes: budget), CancellationToken.None);

        Assert.Equal(invalid, IsInvalidBudget(references));
        Assert.Equal(invalid, IsInvalidBudget(hierarchy));
        Assert.Equal(invalid, IsInvalidBudget(implementations));
        Assert.Equal(invalid, IsInvalidBudget(bodies));
    }

    private static bool IsInvalidBudget(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text.Contains("INVALID_ARGUMENT", System.StringComparison.Ordinal);

    [Fact]
    public async Task FindReferences_FinalBudgetUsesResponseBudgetCompletenessReason()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var original = await FindReferencesTool.ExecuteAsync(
            fixture.CreateServer(), new FindReferencesRequest("IProcessor.Execute", 50, 2, MaxResponseBytes: 65_536), CancellationToken.None);
        var root = JsonNode.Parse(original.StructuredContent!.Value.GetRawText())!.AsObject();
        root["navigation"] = new JsonObject { ["status"] = new JsonObject { ["operation"] = new string('n', 300) } };
        var navigated = McpToolResults.Text(
            Assert.IsType<TextContentBlock>(Assert.Single(original.Content)).Text,
            root);

        var projected = FindReferencesTool.ApplyFinalResponseBudget(navigated, 1_024);
        if (projected.StructuredContent!.Value.TryGetProperty("code", out var code))
        {
            Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", code.GetString());
            return;
        }

        var completeness = projected.StructuredContent!.Value.GetProperty("completeness");
        Assert.True(completeness.GetProperty("truncatedByResponseBudget").GetBoolean());
        Assert.False(completeness.GetProperty("truncatedByMaxResults").GetBoolean());
    }

    [Fact]
    public async Task HierarchyAndImplementations_FinalBudgetKeepNavigationOrReturnMinimumError()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();
        var hierarchy = await GetTypeHierarchyTool.ExecuteAsync(new GetTypeHierarchyRequest(
            server, "IProcessor", 50, McpScopeType.All, false, CancellationToken.None, 65_536));
        var implementations = await FindImplementationsTool.ExecuteAsync(new FindImplementationsRequest(
            server, "IProcessor", 50, McpScopeType.All, false, CancellationToken.None, 65_536));

        AssertFinalBudget(GetTypeHierarchyTool.ApplyFinalResponseBudget, hierarchy);
        AssertFinalBudget(FindImplementationsTool.ApplyFinalResponseBudget, implementations);
    }

    [Fact]
    public async Task FindSymbol_FinalBudgetErrorPublishesAnExecutableMinimumResponseBytes()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var original = await FindSymbolTool.ExecuteAsync(
            fixture.CreateServer(), ["Processor"], kind: null, maxResults: 50, CancellationToken.None);
        var root = JsonNode.Parse(original.StructuredContent!.Value.GetRawText())!.AsObject();
        root["navigation"] = new JsonObject
        {
            ["status"] = new JsonObject { ["operation"] = new string('n', 800) },
        };
        var navigated = McpToolResults.Text(
            Assert.IsType<TextContentBlock>(Assert.Single(original.Content)).Text,
            root);

        var constrained = FindSymbolTool.ApplyFinalResponseBudget(navigated, 512);
        var error = constrained.StructuredContent!.Value;

        Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", error.GetProperty("code").GetString());
        Assert.Equal("$.maxResponseBytes", error.GetProperty("fieldPath").GetString());
        var minimumResponseBytes = error.GetProperty("minimumResponseBytes").GetInt32();
        Assert.True(minimumResponseBytes > 512);

        var retry = FindSymbolTool.ApplyFinalResponseBudget(navigated, minimumResponseBytes);
        Assert.False(retry.StructuredContent!.Value.TryGetProperty("code", out _));
        Assert.True(
            Encoding.UTF8.GetByteCount(Assert.IsType<TextContentBlock>(Assert.Single(retry.Content)).Text)
            + Encoding.UTF8.GetByteCount(retry.StructuredContent!.Value.GetRawText()) <= minimumResponseBytes);
    }

    private static void AssertFinalBudget(Func<CallToolResult, int, CallToolResult> apply, CallToolResult original)
    {
        var root = JsonNode.Parse(original.StructuredContent!.Value.GetRawText())!.AsObject();
        root["navigation"] = new JsonObject { ["status"] = new JsonObject { ["operation"] = new string('n', 300) } };
        var navigated = McpToolResults.Text(Assert.IsType<TextContentBlock>(Assert.Single(original.Content)).Text, root);
        var projected = apply(navigated, 1_024);
        if (projected.StructuredContent!.Value.TryGetProperty("code", out var code))
        {
            Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", code.GetString());
            return;
        }

        Assert.True(Encoding.UTF8.GetByteCount(Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text)
            + Encoding.UTF8.GetByteCount(projected.StructuredContent!.Value.GetRawText()) <= 1_024);
        Assert.True(projected.StructuredContent!.Value.TryGetProperty("navigation", out _));
    }
}
