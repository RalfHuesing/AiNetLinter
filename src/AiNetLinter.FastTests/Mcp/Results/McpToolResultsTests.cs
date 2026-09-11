#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Results;

[Trait("Category", "Unit")]
public sealed partial class McpToolResultsTests
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
    public void WithNavigation_ConfigurationErrorsUseCatalogErrorStatus()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-config-error-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Recoverable(ProjectErrorCodes.RulesInvalid, "Regelkonfiguration ist ungültig."),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
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
        Assert.Equal("source", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.True(navigation.GetProperty("snapshot").GetProperty("fresh").GetBoolean());
        Assert.Equal(1, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("source", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("empty", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("refine_scope", navigation.GetProperty("next").GetProperty("kind").GetString());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Status: operation=ok, completeness=empty", text, StringComparison.Ordinal);
        Assert.Contains("keine Treffer im Scope", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Navigation", text, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.ValueKind);
    }

    [Fact]
    public void WithNavigation_PreservesNestedSectionNextStepWhenRootIsComplete()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-section-next-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Feature-Kontext",
                new
                {
                    completeness = "complete",
                    testContext = new
                    {
                        completeness = "complete",
                        nextStep = "Abschnitt testContext: Detail erneut anfordern.",
                    },
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("complete", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal(
            "Abschnitt testContext: Detail erneut anfordern.",
            navigation.GetProperty("next").GetProperty("action").GetString());
    }

    [Fact]
    public void WithNavigation_WireTruncationOverridesExistingCompleteNavigation()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-wire-truncated-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Wire gekürzt",
                new
                {
                    navigation = new { completeness = "complete" },
                    wireTruncated = true,
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
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
