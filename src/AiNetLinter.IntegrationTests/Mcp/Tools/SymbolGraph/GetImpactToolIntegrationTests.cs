#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Integration")]
public sealed class GetImpactToolIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_GitRefUncommittedChange_StructuredContentDeserializesToCallSiteEntries()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.True(result.StructuredContent!.Value.TryGetProperty("callSites", out var entries));
        Assert.Contains("CalculatorCaller.cs", entries.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvableGitRef_ReturnsRecoverableAnalysisFailedNotEmptyResult()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput("does-not-exist-xyz", null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("ANALYSIS_FAILED", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 2, 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "ValidClassA.DoWork", 50, 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
    }
}
