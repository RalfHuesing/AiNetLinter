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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "irrelevant", 50, 1), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsRecoverableInvalidArgument()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput("HEAD~1", "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StableSymbolIdentifierGiven_ReturnsCallSites()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, stableId, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbolIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "DoesNotExistXyz", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("CalculatorCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvableGitRef_ReturnsRecoverableAnalysisFailedNotEmptyResult()
    {
        // isError-Policy: eine nicht aufloesende gitRef ist ein behebbarer Nutzereingabe-Fehler
        // (Tippfehler, falscher Branch-Name) — IsError bleibt false, siehe GetImpactTool.
        using var fixture = new GitImpactMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput("does-not-exist-xyz", null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ANALYSIS_FAILED", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("does-not-exist-xyz", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine betroffenen Aufrufstellen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine betroffenen Aufrufstellen gefunden", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 2, 1), CancellationToken.None);

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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, null, 2, 1), CancellationToken.None);

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
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "ValidClassA.DoWork", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierWithDepth2_StillReturnsCallSite()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 50, 2), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }
}
