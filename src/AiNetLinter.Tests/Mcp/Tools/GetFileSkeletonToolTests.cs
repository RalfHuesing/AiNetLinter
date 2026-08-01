using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class GetFileSkeletonToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(null);

        var result = await GetFileSkeletonTool.ExecuteAsync(state, "irrelevant.cs", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilePath_ReturnsResourceNotFoundError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/DoesNotExist.cs", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

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
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(catalog);

        var relativeResult = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/SymbolGraphMini/Greeter.cs", CancellationToken.None);
        var absoluteResult = await GetFileSkeletonTool.ExecuteAsync(
            state, fixture.GreeterPath, CancellationToken.None);

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
        using var state = new McpCodeGraphServer(catalog);

        // BrokenClassA.cs hat einen Syntax-Fehler (offene Klammer in Methodensignatur) — der
        // datei-spezifische Warnhinweis muss erscheinen, weil das Skelett unvollstaendig ist.
        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/CompileErrorMini/BrokenClassA.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        // Datei-spezifischer Hinweis (NICHT Aggregate-Format "N Dateien mit Compile-Fehlern").
        Assert.Contains("Diese Datei hat", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
        // Diagnostics-IDs (CS1513 etc.) koennen im datei-spezifischen Format vorkommen.
        Assert.Matches(@"CS\d{4}", text);
    }
}
