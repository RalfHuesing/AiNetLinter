using System;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class FindReferencesToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(null);

        var result = await FindReferencesTool.ExecuteAsync(state, "irrelevant", maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_QualifiedName_ReturnsSingleMatch()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(catalog.Solution, "Greeter.Greet", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_UnknownName_ReturnsSymbolNotFoundError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(catalog.Solution, "DoesNotExistXyz", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(catalog.Solution, "Run", CancellationToken.None);

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
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var identifier = $"{fixture.GreeterPath}:5:19";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(catalog.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        // Caller.cs hat nach Fixture-Erweiterung 5 Greet-Aufrufe (1 in Run + 2 in RunTwice + 3 in RunThrice)
        // ueber die Roslyn-Call-Site-API; maxResults: 2 erzwingt Trunkierung mit Meta-Zeile.
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
        using var state = new McpCodeGraphServer(catalog);

        // ValidClassA.DoWork existiert in der CompileErrorMini-Fixture ohne Aufrufstellen — daher
        // greift der "Keine Aufrufstellen"-Pfad. Der Aggregate-Warnhinweis muss trotzdem davor stehen.
        var result = await FindReferencesTool.ExecuteAsync(state, "ValidClassA.DoWork", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }
}
