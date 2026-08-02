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

public sealed class FindReferencesToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public FindReferencesToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(null));

        var result = await FindReferencesTool.ExecuteAsync(state, "irrelevant", maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_QualifiedName_ReturnsSingleMatch()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_UnknownName_ReturnsSymbolNotFoundError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "DoesNotExistXyz", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "Run", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("OtherCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionIdentifier_ReturnsSymbolAtPosition()
    {
        var identifier = $"{_fixture.Workspace.GreeterPath}:5:19";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 2, CancellationToken.None);

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

        var result = await FindReferencesTool.ExecuteAsync(state, "ValidClassA.DoWork", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }
}
