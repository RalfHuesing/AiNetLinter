#nullable enable

using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpToolResultsTests
{
    [Fact]
    public void Error_BuildsIsErrorResultWithFormattedText()
    {
        var result = McpToolResults.Error("TEST_CODE", "Testnachricht");

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("[ERROR]: TEST_CODE: Testnachricht", textContent.Text);
    }

    [Fact]
    public void SolutionNotLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var result = McpToolResults.SolutionNotLoaded();

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public void Text_BuildsNonErrorResultWithGivenText()
    {
        var result = McpToolResults.Text("Hallo");

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("Hallo", textContent.Text);
    }

    [Fact]
    public void CompilationError_ReturnsErrorWithWorkspaceDiagnosticCode()
    {
        var result = McpToolResults.CompilationError("Compile-Fehler blockieren Aufloesung", context: "BrokenClassA");

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("WORKSPACE_DIAGNOSTIC", textContent.Text);
        Assert.Contains("BrokenClassA", textContent.Text);
    }
}
