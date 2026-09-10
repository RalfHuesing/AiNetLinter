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
        Assert.Equal("error", navigation.GetProperty("operationStatus").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("completeness").GetString());
        Assert.False(navigation.GetProperty("result").GetProperty("available").GetBoolean());
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

        Assert.Equal("complete", navigation.GetProperty("completeness").GetString());
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
        Assert.Equal("truncated", navigation.GetProperty("completeness").GetString());
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
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("completeness").GetString());
    }

    [Fact]
    public void WithNavigation_WireTruncationOverridesFeaturePartialButPreservesFailureCause()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-feature-partial-wire-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var budgeted = McpToolResults.ApplyCompositeWireBudget(
            McpToolResults.Text(
                "Feature-Kontext",
                new
                {
                    completeness = "partial",
                    impact = new
                    {
                        callSites = Enumerable.Range(0, 200)
                            .Select(index => new
                            {
                                filePath = $"src/Caller{index:D3}.cs",
                                line = index + 1,
                                details = new string('ä', 80),
                            }),
                        totalCallers = 200,
                        completeness = "complete",
                    },
                    violations = new
                    {
                        status = "error",
                        reasonCode = "violations-scan-failed",
                    },
                }),
            ["impact", "violations"]);

        var result = McpToolResults.WithNavigation(budgeted, target);
        var payload = result.StructuredContent!.Value;

        Assert.True(payload.GetProperty("wireBudget").GetProperty("truncated").GetBoolean());
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("completeness").GetString());
        Assert.Equal("partial", payload.GetProperty("completeness").GetString());
        Assert.Equal("error", payload.GetProperty("violations").GetProperty("status").GetString());
        Assert.Equal(
            "violations-scan-failed",
            payload.GetProperty("violations").GetProperty("reasonCode").GetString());
    }

    [Theory]
    [InlineData("find_references")]
    [InlineData("get_impact")]
    public void WithNavigation_SymbolTraversalMaxResultsProjectsTruncation(string toolName)
    {
        using var tempDir = TestTempDirectory.Create($"mcp-navigation-symbol-truncated-{toolName}-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        // Beide Symbolgraph-Tools liefern bei maxResults < Trefferzahl denselben
        // TraversalCompleteness-Vertrag: die Trefferliste ist gekuerzt, weitere Daten
        // sind ueber einen erneuten/erweiterten Call anzufordern.
        var result = McpToolResults.Text(
            $"{toolName}: 1 von 3 Treffern",
            new
            {
                callSites = new[] { new { filePath = "Caller.cs", line = 1 } },
                completeness = new
                {
                    requestedDepth = 1,
                    effectiveDepth = 1,
                    visitedNodeCount = 3,
                    totalCallSiteCount = 3,
                    shownCallSiteCount = 1,
                    truncatedByMaxResults = true,
                    truncatedByNodeLimit = false,
                    depthWasClamped = false,
                },
            });

        var navigated = McpToolResults.WithNavigation(result, target);
        var navigation = navigated.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("truncated", navigation.GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("not_decidable")]
    [InlineData("truncated")]
    public void WithNavigation_PromotesSummaryCompleteness(string expectedCompleteness)
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-summary-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Audit",
                new { summary = new { completeness = expectedCompleteness } }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal(expectedCompleteness, navigation.GetProperty("completeness").GetString());
    }

    [Fact]
    public void WithNavigation_DecisionableEmptyViolationListRemainsComplete()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-safeguard-empty-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Safeguard",
                new
                {
                    passed = true,
                    score = 10.0,
                    violations = Array.Empty<object>(),
                    completeness = "complete",
                    status = "passed",
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("complete", navigation.GetProperty("completeness").GetString());
        Assert.True(navigation.GetProperty("result").GetProperty("available").GetBoolean());
        Assert.Equal("none", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Fact]
    public void WithNavigation_UsesSummaryNextReasonAsSafeFollowUp()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-summary-next-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Audit",
                new
                {
                    summary = new
                    {
                        status = "checked",
                        next = new { action = "countercheck", reason = "Kandidaten manuell prüfen." },
                    },
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal(
            "Kandidaten manuell prüfen.",
            navigation.GetProperty("next").GetProperty("action").GetString());
    }

    [Fact]
    public void WithNavigation_FeatureContextSectionFailureIsNotAvailable()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-feature-error-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "Feature-Kontext",
                new
                {
                    completeness = "partial",
                    violations = new
                    {
                        status = "partial",
                        reasonCode = "violations-scan-failed",
                        nextStep = "Abschnitt violations: erneut anfordern.",
                    },
                }),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("error", navigation.GetProperty("operationStatus").GetString());
        Assert.Equal("partial", navigation.GetProperty("completeness").GetString());
        Assert.False(navigation.GetProperty("result").GetProperty("available").GetBoolean());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal(
            "Abschnitt violations: erneut anfordern.",
            navigation.GetProperty("next").GetProperty("action").GetString());
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
