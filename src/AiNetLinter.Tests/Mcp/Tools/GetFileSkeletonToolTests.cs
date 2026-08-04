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

public sealed class GetFileSkeletonToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetFileSkeletonToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetFileSkeletonTool.ExecuteAsync(state, "irrelevant.cs", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilePath_ReturnsResourceNotFoundError()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/DoesNotExist.cs", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/Greeter.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Caller", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OtherCaller", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AbsolutePath_ResolvesSameAsRelativePath()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var relativeResult = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/Greeter.cs", CancellationToken.None);
        var absoluteResult = await GetFileSkeletonTool.ExecuteAsync(
            state, _fixture.Workspace.GreeterPath, CancellationToken.None);

        Assert.NotEqual(true, relativeResult.IsError);
        Assert.NotEqual(true, absoluteResult.IsError);
        var relativeText = Assert.IsType<TextContentBlock>(Assert.Single(relativeResult.Content)).Text;
        var absoluteText = Assert.IsType<TextContentBlock>(Assert.Single(absoluteResult.Content)).Text;
        Assert.Contains("Greet", relativeText, StringComparison.Ordinal);
        Assert.Contains("Greet", absoluteText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFile_OutputContainsFileSpecificWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/CompileErrorMini/BrokenClassA.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Diese Datei hat", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
        Assert.Matches(@"CS\d{4}", text);
    }
}
