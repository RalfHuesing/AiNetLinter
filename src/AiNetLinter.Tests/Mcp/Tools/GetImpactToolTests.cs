using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class GetImpactToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetImpactToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(null));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "irrelevant", maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsInvalidArgumentError()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: "HEAD~1", symbolIdentifier: "Greeter.Greet", maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "Greeter.Greet", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbolIdentifier_ReturnsSymbolNotFoundError()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "DoesNotExistXyz", maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("CalculatorCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine betroffenen Aufrufstellen gefunden", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetImpactTool.ExecuteAsync(
            state, gitRef: null, symbolIdentifier: "Greeter.Greet", maxResults: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog));

        var result = await GetImpactTool.ExecuteAsync(
            state, gitRef: null, symbolIdentifier: null, maxResults: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog));

        var result = await GetImpactTool.ExecuteAsync(
            state, gitRef: null, symbolIdentifier: "ValidClassA.DoWork", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }
}
