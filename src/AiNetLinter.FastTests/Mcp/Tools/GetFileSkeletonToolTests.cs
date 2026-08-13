#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class GetFileSkeletonToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetFileSkeletonToolTests() { _fixture = new McpInMemoryTestContext(); }

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
    public async Task ExecuteAsync_UnknownFilePath_ReturnsRecoverableResourceNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/DoesNotExist.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod()
    {
        var state = _fixture.CreateServer();

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
        var state = _fixture.CreateServer();

        var relativeResult = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/Greeter.cs", CancellationToken.None);
        var absoluteResult = await GetFileSkeletonTool.ExecuteAsync(
            state, SymbolGraphMiniSolutionSpec.GreeterPath, CancellationToken.None);

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
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/CompileErrorMini/BrokenClassA.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Diese Datei hat", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
        Assert.Matches(@"CS\d{4}", text);
    }
}
