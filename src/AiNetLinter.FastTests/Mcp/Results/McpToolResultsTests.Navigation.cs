#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Results;

public sealed partial class McpToolResultsTests
{
    [Fact]
    public void WithNavigation_CompleteSuccessHasNoTextNavigationFooter()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-text-complete-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("body", new { completeness = "complete" }),
            target);

        Assert.Equal("body", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.DoesNotContain("Navigation", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithNavigation_EmptyIncludesScopeAndCountHint()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-text-empty-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "body",
                new
                {
                    completeness = "empty",
                    scope = new { requestedType = "production", includeGenerated = false },
                    totalCount = 0,
                    shownCount = 0,
                }),
            target);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("production", text, StringComparison.Ordinal);
        Assert.Contains("0", text, StringComparison.Ordinal);
        Assert.Equal("empty", result.StructuredContent!.Value.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Theory]
    [InlineData("{\"completeness\":\"empty\",\"scope\":{\"effectiveScope\":\"src/empty\"},\"totalCount\":0}", "src/empty", 0)]
    [InlineData("{\"summary\":{\"completeness\":\"empty\",\"scope\":\"production\",\"total\":7}}", "production", 7)]
    [InlineData("{\"completeness\":{\"status\":\"empty\",\"totalCount\":4}}", "angeforderter Scope", 4)]
    public void WithNavigation_EmptyReadsNestedScopeAndCountShapes(string json, string expectedScope, int expectedTotal)
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-text-empty-shapes-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var payload = JsonSerializer.Deserialize<JsonElement>(json);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text("body", payload),
            target);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains($"keine Treffer im Scope {expectedScope} (0 von {expectedTotal} Treffern).", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNavigation_TruncatedIncludesExactlyOneActionWithoutBlankSeparator()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-text-truncated-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "body",
                new
                {
                    completeness = "truncated",
                    totalCount = 3,
                    shownCount = 1,
                }),
            target);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("\n\n", text, StringComparison.Ordinal);
        Assert.Contains("truncated", text, StringComparison.Ordinal);
        Assert.Contains("maxResults", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split('\n').Length);
        Assert.Equal("truncated", result.StructuredContent!.Value.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Fact]
    public void WithNavigation_RecoverableUsesStatusAndOneActionWithoutIdentifierEcho()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-text-recoverable-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Recoverable("INVALID_ARGUMENT", "Argument ist ungueltig.", hint: "Scope verfeinern."),
            target);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("operation=error", text, StringComparison.Ordinal);
        Assert.Contains("Scope verfeinern", text, StringComparison.Ordinal);
        Assert.DoesNotContain(target.CanonicalPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsError);
        Assert.Equal("error", result.StructuredContent!.Value.GetProperty("navigation").GetProperty("status").GetProperty("operation").GetString());
    }

    [Fact]
    public void WithNavigation_ProjectsOnlyTheV2Envelope()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-envelope-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target) with
        {
            AnalysisSnapshotFingerprint = new string('a', 64),
            AnalysisSnapshotKind = "source",
            AnalysisSnapshotFresh = true,
        };

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "ok",
                new
                {
                    completeness = "complete",
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("source", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal(target.CanonicalPath, navigation.GetProperty("target").GetProperty("targetPath").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("complete", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(JsonValueKind.Null, navigation.GetProperty("next").ValueKind);
    }

    [Fact]
    public void WithNavigation_PreservesApplicableScopeAndHandoffContract()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-optional-contract-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Text(
                "ok",
                new
                {
                    completeness = "complete",
                    scope = new
                    {
                        requestedType = "all",
                        includeGenerated = false,
                    },
                    handoff = new
                    {
                        acceptedAs = "symbolIdentifier",
                        followUpsByKind = new
                        {
                            type = new[] { "get_type_hierarchy", "find_implementations" },
                            member = new[] { "get_symbol_body", "find_references", "get_call_tree" },
                        },
                    },
                }),
            target);

        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        var scope = navigation.GetProperty("scope");
        Assert.Equal("all", scope.GetProperty("requestedType").GetString());
        Assert.False(scope.GetProperty("includeGenerated").GetBoolean());

        var handoff = navigation.GetProperty("handoff");
        Assert.Equal("symbolIdentifier", handoff.GetProperty("acceptedAs").GetString());
        Assert.Equal(
            new[] { "get_type_hierarchy", "find_implementations" },
            handoff.GetProperty("followUpsByKind").GetProperty("type").Deserialize<string[]>());
        Assert.Equal(
            new[] { "get_symbol_body", "find_references", "get_call_tree" },
            handoff.GetProperty("followUpsByKind").GetProperty("member").Deserialize<string[]>());
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

        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("partial")]
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
        Assert.Equal(expectedCompleteness, navigation.GetProperty("status").GetProperty("completeness").GetString());
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
        Assert.Equal("complete", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(JsonValueKind.Null, navigation.GetProperty("next").ValueKind);
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

        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
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

        Assert.True(result.IsError);
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Fact]
    public void WithNavigation_InvalidAssemblyProjectsRefineScope()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-invalid-asm-");
        var dllPath = Path.Combine(tempDir.DirectoryPath, "corrupt.dll");
        File.WriteAllText(dllPath, "not a real PE file");
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(dllPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.InvalidAssembly("Die angegebene Datei ist keine gültige .NET-Assembly.", "Anderes Target wählen."),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.StartsWith("Keine Wiederholung nötig", navigation.GetProperty("next").GetProperty("action").GetString());
    }

    [Fact]
    public void WithNavigation_FileTreeTruncated_ProjectsMatchingToolOwnedNext()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-filetree-next-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.Text(
            "get_file_tree: root=. view=files",
            new
            {
                fileTree = new
                {
                    completeness = new
                    {
                        scanCompleted = true,
                        truncated = true,
                        truncatedBy = new[] { "maxResults" }
                    },
                    next = new
                    {
                        kind = "refine_scope",
                        action = "root/fileFilter verfeinern oder maxResults/maxResponseBytes erhöhen."
                    }
                }
            });

        var navigated = McpToolResults.WithNavigation(result, target);
        var navigation = navigated.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("refine_scope", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal("root/fileFilter verfeinern oder maxResults/maxResponseBytes erhöhen.", navigation.GetProperty("next").GetProperty("action").GetString());
    }

    [Fact]
    public void WithNavigation_ProjectTargetUnsupported_ProjectsUnsupportedOperationStatusAndNext()
    {
        using var tempDir = TestTempDirectory.Create("mcp-navigation-unsupported-project-");
        var solutionPath = Path.Combine(tempDir.DirectoryPath, "workspace.slnx");
        File.WriteAllText(solutionPath, string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);

        var result = McpToolResults.WithNavigation(
            McpToolResults.Recoverable(
                AiNetLinter.Output.LinterErrorCodes.ProjectTargetUnsupported,
                "Dieses Tool unterstützt kein Projekt-Ziel.",
                hint: "targetPath auf eine vorhandene .dll/.exe-Datei setzen und eine Assembly-Operation verwenden."),
            target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal(AiNetLinter.Output.LinterErrorCodes.ProjectTargetUnsupported, navigation.GetProperty("status").GetProperty("code").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
    }
}
