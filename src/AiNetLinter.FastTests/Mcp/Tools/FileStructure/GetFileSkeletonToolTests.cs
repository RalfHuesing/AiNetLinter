#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class GetFileSkeletonToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetFileSkeletonToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetFileSkeletonTool.ExecuteAsync(state, ["irrelevant.cs"], CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    public static IEnumerable<object?[]> EmptyCases =>
    [
        [null],
        [System.Array.Empty<string>()],
        [new[] { "", "   " }]
    ];

    [Theory]
    [MemberData(nameof(EmptyCases))]
    public async Task ExecuteAsync_EmptyFilePaths_ReturnsRecoverableInvalidArgument(string[]? filePaths)
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(state, filePaths, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("Pflichtparameter 'filePaths' fehlt oder ist leer.", textContent.Text);
        Assert.Contains("filePaths: [\"src/MyClass.cs\"]", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilePath_ReturnsRecoverableResourceNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/DoesNotExist.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Erzeugt:", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Caller", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OtherCaller", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AbsolutePath_ResolvesSameAsRelativePath()
    {
        var state = _fixture.CreateServer();

        var relativeResult = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);
        var absoluteResult = await GetFileSkeletonTool.ExecuteAsync(
            state, [SymbolGraphMiniSolutionSpec.GreeterPath], CancellationToken.None);

        Assert.NotEqual(true, relativeResult.IsError);
        Assert.NotEqual(true, absoluteResult.IsError);
        var relativeText = Assert.IsType<TextContentBlock>(Assert.Single(relativeResult.Content)).Text;
        var absoluteText = Assert.IsType<TextContentBlock>(Assert.Single(absoluteResult.Content)).Text;
        Assert.Contains("Greet", relativeText, StringComparison.Ordinal);
        Assert.Contains("Greet", absoluteText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFile_ReturnsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/CompileErrorMini/BrokenClassA.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFiles_ReturnsCombinedSkeletonsInSingleTurn()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state,
            filePaths: ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/Caller.cs"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
        Assert.Contains("Caller", text, StringComparison.Ordinal);
        Assert.Contains("---", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFiles_WithOneNotFound_ContinuesAndIncludesWarning()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state,
            filePaths: ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/NonExistent.cs"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
        Assert.Contains("Datei nicht gefunden: `src/SymbolGraphMini/NonExistent.cs`", text, StringComparison.Ordinal);
    }
}
