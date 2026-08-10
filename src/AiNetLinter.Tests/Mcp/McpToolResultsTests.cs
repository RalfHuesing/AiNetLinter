#nullable enable

using System.Collections.Generic;
using System.Text.Json;
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
    public void Text_WithListPayload_StructuredContentIsJsonObjectNotArray()
    {
        // Regression: das MCP-Protokoll verlangt structuredContent als JSON-Objekt. Ein nacktes
        // Array (z. B. eine Liste direkt als payload) liess reale MCP-Clients den gesamten
        // Tool-Call schema-seitig ablehnen (betraf get_violations, get_hotspots,
        // get_index_scope, find_symbol, find_references, get_impact bis zum Fix) — siehe
        // McpToolResults.Text``1-Doc-Kommentar. Payload hier bewusst gewrappt, wie es alle
        // Tool-Call-Sites seit dem Fix tun.
        var result = McpToolResults.Text("Hallo", new { Items = new List<int> { 1, 2, 3 } });

        Assert.NotNull(result.StructuredContent);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);
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
