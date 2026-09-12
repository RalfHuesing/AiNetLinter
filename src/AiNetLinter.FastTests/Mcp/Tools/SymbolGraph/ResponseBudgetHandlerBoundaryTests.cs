#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Wire;
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
    public void FindReferences_FinalBudgetReturnsVisibleRecoveryForOversizedContent()
    {
        var projected = FindReferencesTool.ApplyFinalResponseBudget(McpToolResults.Text(new string('x', 2_048)), 1_024);
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", TextOf(projected), StringComparison.Ordinal);
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
    public async Task FindSymbol_FinalBudgetKeepsVisibleTextOrReturnsRecovery()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var original = await FindSymbolTool.ExecuteAsync(
            fixture.CreateServer(), ["Processor"], kind: null, maxResults: 50, CancellationToken.None);
        var constrained = FindSymbolTool.ApplyFinalResponseBudget(original, 512);
        var text = TextOf(constrained);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= 512 || text.Contains("RESPONSE_BUDGET_TOO_SMALL", StringComparison.Ordinal));
    }

    private static void AssertFinalBudget(Func<CallToolResult, int, CallToolResult> apply, CallToolResult original)
    {
        var projected = apply(original, 1_024);
        var text = TextOf(projected);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= 1_024 || text.Contains("RESPONSE_BUDGET_TOO_SMALL", StringComparison.Ordinal));
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
