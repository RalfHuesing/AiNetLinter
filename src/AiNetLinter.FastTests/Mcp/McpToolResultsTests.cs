#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

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

    [Fact]
    public void WithNavigation_AddsStableTargetSnapshotCapabilitiesAndCompleteness()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var analysisSnapshot = new string('a', 64);
        target = target with
        {
            AnalysisSnapshotFingerprint = analysisSnapshot,
            AnalysisSnapshotKind = "source-files",
            AnalysisSnapshotFresh = true,
        };

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("Keine Treffer", new { Matches = Array.Empty<object>() }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal(target.CanonicalPath, navigation.GetProperty("target").GetProperty("targetPath").GetString());
        Assert.Equal(target.AnalysisRoot, navigation.GetProperty("target").GetProperty("analysisRoot").GetString());
        Assert.Equal(analysisSnapshot, navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString());
        Assert.NotEqual(target.Fingerprint, navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString());
        Assert.Equal("source-files", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.True(navigation.GetProperty("snapshot").GetProperty("fresh").GetBoolean());
        Assert.Equal("source", navigation.GetProperty("origin").GetString());
        Assert.Equal("supported", navigation.GetProperty("capabilities").GetProperty("navigation").GetString());
        Assert.Equal("not_configured", navigation.GetProperty("capabilities").GetProperty("lint").GetString());
        Assert.Equal("ok", navigation.GetProperty("operationStatus").GetString());
        Assert.Equal("empty", navigation.GetProperty("completeness").GetString());
        Assert.False(navigation.GetProperty("result").GetProperty("available").GetBoolean());
        Assert.Equal("refine_scope", navigation.GetProperty("next").GetProperty("kind").GetString());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("operationStatus: `ok`", text, StringComparison.Ordinal);
        Assert.Contains("completeness: `empty`", text, StringComparison.Ordinal);
        Assert.Contains("next: `refine_scope`", text, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.ValueKind);
    }

    [Fact]
    public void WithNavigation_SymbolMissIsNotProjectedAsSuccessfulCompleteResult()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-miss-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.SymbolNotFound("M:Missing.Type.Member"),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("symbol_not_found", navigation.GetProperty("operationStatus").GetString());
        Assert.False(navigation.GetProperty("result").GetProperty("available").GetBoolean());
        Assert.Equal("not_applicable", navigation.GetProperty("completeness").GetString());
        Assert.Equal("refine_scope", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Fact]
    public void InvalidArgument_PreservesFieldPathInStructuredError()
    {
        var result = McpToolResults.InvalidArgument(
            "Unbekanntes Argument: projectRoot",
            fieldPath: "$.projectRoot");

        Assert.Equal("$.projectRoot", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }
}
