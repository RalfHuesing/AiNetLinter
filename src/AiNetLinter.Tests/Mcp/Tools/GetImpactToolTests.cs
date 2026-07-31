using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class GetImpactToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(null);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "irrelevant", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsInvalidArgumentError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: "HEAD~1", symbolIdentifier: "Greeter.Greet", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "Greeter.Greet", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbolIdentifier_ReturnsSymbolNotFoundError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: "DoesNotExistXyz", CancellationToken.None);

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
        var state = new McpCodeGraphServer(catalog);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("CalculatorCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await GetImpactTool.ExecuteAsync(state, gitRef: null, symbolIdentifier: null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine betroffenen Aufrufstellen gefunden", textContent.Text, StringComparison.Ordinal);
    }
}
