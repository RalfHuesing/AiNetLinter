#nullable enable

using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

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
    public void WarningsSection_ReturnsWarningTextUnchanged_ForConcatenationByTool()
    {
        // EPIC-06-Aggregat-Helper: liefert den Hint-Text unveraendert zurueck, damit der
        // Aufrufer ihn vor den eigentlichen Output konkatenieren kann. Kein CallToolResult
        // bewusst — der Tool-Output wird zu einem einzigen Text-Content-Block zusammengefuehrt
        // (eine Code-Stelle, ein Format, kein Multi-Block-Result-Building in jedem Tool).
        var warning = "Hinweis: 3 Dateien mit Compile-Fehlern";
        var result = McpToolResults.WarningsSection(warning);

        Assert.Equal(warning, result);
        // Symbolischer A3-Schutz: ohne diese Methode wuerde der Aufruf nicht kompilieren,
        // daher ist die Kompilierbarkeit selbst der Nachweis, dass der Test ohne Implementation rot wird.
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
