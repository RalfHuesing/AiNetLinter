#nullable enable

using System;
using System.IO;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Results;

public sealed partial class McpToolResultsTests
{
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
}
