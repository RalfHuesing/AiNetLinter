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
    public void ApplyCompositeWireBudget_TrimsBothCompositeShapesWithinUtf8Budgets()
    {
        var callers = Enumerable.Range(0, 80)
            .Select(index => new
            {
                filePath = $"src/Caller{index:D2}.cs",
                line = index + 1,
                callerMemberName = $"Caller{index:D2}.Run",
                projectName = "AiNetLinter",
                details = new string('ä', 160),
            })
            .ToArray();
        var testFiles = Enumerable.Range(0, 80)
            .Select(index => new
            {
                filePath = $"tests/Feature{index:D2}Tests.cs",
                testClassName = $"Feature{index:D2}Tests",
                category = "Unit",
                matchReason = "symbol-reference",
                testMethods = Enumerable.Range(0, 12).Select(method => $"Run_{index:D2}_{method:D2}").ToArray(),
                totalClassTests = 12,
                totalMatchingMethods = 12,
            })
            .ToArray();

        var feature = McpToolResults.ApplyCompositeWireBudget(
            McpToolResults.Text(
                new string('x', 20_000),
                new
                {
                    declaration = new { name = "Feature.Run", kind = "Method" },
                    metrics = new { metric = "complexity", value = 3 },
                    impact = new
                    {
                        totalCallers = callers.Length,
                        callSites = callers,
                        isTruncated = false,
                        completeness = "complete",
                    },
                    testContext = new
                    {
                        totalMatchingTests = testFiles.Length * 12,
                        totalTestFiles = testFiles.Length,
                        testFiles,
                        isTruncated = false,
                        completeness = "complete",
                    },
                    violations = new
                    {
                        totalViolationsOnFile = 0,
                        violations = Array.Empty<object>(),
                        status = "complete",
                        isTruncated = false,
                    },
                    completeness = "complete",
                }),
            ["declaration", "metrics", "impact", "testContext", "violations"]);

        var testContext = McpToolResults.ApplyCompositeWireBudget(
            McpToolResults.Text(
                new string('y', 20_000),
                new
                {
                    targetSymbol = "Feature.Run",
                    targetKind = "Method",
                    targetFilePath = "src/Feature.cs",
                    totalMatchingTests = testFiles.Length * 12,
                    totalTestFiles = testFiles.Length,
                    testFiles,
                    recommendedTestCommands = Enumerable.Range(0, 80)
                        .Select(index => $"dotnet test --filter Feature{index:D2}")
                        .ToArray(),
                    isUntested = false,
                    isTruncated = false,
                    completeness = "complete",
                }),
            ["testContext"],
            rootSectionName: "testContext");

        AssertCompositeWireBudget(feature, "impact", "testContext");
        AssertCompositeWireBudget(testContext, "testContext");
        Assert.Contains("Wire-Budget", Assert.IsType<TextContentBlock>(Assert.Single(feature.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("responseBudget", feature.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("Wire-Budget", Assert.IsType<TextContentBlock>(Assert.Single(testContext.Content)).Text, StringComparison.Ordinal);
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
    public void ApplyCompositeWireBudget_UpdatesExistingNavigationOnResponseBudgetTruncation()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-composite-wire-budget-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var navigated = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Feature",
                new
                {
                    impact = new
                    {
                        callSites = Enumerable.Range(0, 200)
                            .Select(index => new { filePath = $"src/Caller{index:D3}.cs", line = index + 1 }),
                        completeness = "complete",
                    },
                }),
            target);

        var budgeted = McpToolResults.ApplyCompositeWireBudget(navigated, ["impact"]);
        var payload = budgeted.StructuredContent!.Value;

        Assert.True(payload.GetProperty("wireBudget").GetProperty("truncated").GetBoolean());
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Fact]
    public void InvalidArgument_PreservesFieldPathInStructuredError()
    {
        var result = McpToolResults.InvalidArgument(
            "Unbekanntes Argument: projectRoot",
            fieldPath: "$.projectRoot");

        Assert.Equal("$.projectRoot", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public void ApplyCompositeWireBudget_RepeatedApplication_DoesNotDuplicateNextStepNotice()
    {
        var sectionContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}: excessive payload data"));
        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["impact"] = new System.Text.Json.Nodes.JsonObject
            {
                ["details"] = sectionContent,
                ["completeness"] = "complete"
            }
        };
        var result = McpToolResults.Text("Test", payload);
        var budgeted1 = McpToolResults.ApplyCompositeWireBudget(result, ["impact"]);
        var budgeted2 = McpToolResults.ApplyCompositeWireBudget(budgeted1, ["impact"]);

        var nextStep = budgeted2.StructuredContent!.Value.GetProperty("impact").GetProperty("nextStep").GetString()!;
        var occurrences = (nextStep.Length - nextStep.Replace("Wire-Budget", "").Length) / "Wire-Budget".Length;
        Assert.Equal(1, occurrences);
    }

    private static void AssertCompositeWireBudget(CallToolResult result, params string[] sectionNames)
    {
        var structured = result.StructuredContent!.Value;
        Assert.Equal(JsonValueKind.Object, structured.ValueKind);
        var wireBudget = structured.GetProperty("wireBudget");
        Assert.True(
            wireBudget.GetProperty("totalBytes").GetInt32() <= McpToolResults.CompositeWireBudgetBytes,
            wireBudget.GetRawText());
        Assert.True(wireBudget.GetProperty("structuredBytes").GetInt32() <= McpToolResults.CompositeWireBudgetBytes);

        foreach (var sectionName in sectionNames)
        {
            var section = sectionName == "testContext"
                && !structured.TryGetProperty("testContext", out _)
                ? structured
                : structured.GetProperty(sectionName);
            Assert.True(
                Encoding.UTF8.GetByteCount(section.GetRawText()) <= McpToolResults.CompositeSectionBudgetBytes,
                $"Abschnitt {sectionName} überschreitet das Wirebudget.");
            Assert.Equal("truncated", section.GetProperty("completeness").GetString());
            Assert.True(section.GetProperty("isTruncated").GetBoolean());
            Assert.Contains(
                "responseBudget",
                section.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
            Assert.False(string.IsNullOrWhiteSpace(section.GetProperty("nextStep").GetString()));
        }
    }
}
