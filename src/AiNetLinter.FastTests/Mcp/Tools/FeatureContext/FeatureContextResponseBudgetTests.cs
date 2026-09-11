#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Component")]
public sealed class FeatureContextResponseBudgetTests
{
    [Fact]
    public void Apply_HotSymbolKeepsMinimumProjectionAndConsistentCounts()
    {
        var payload = CreatePayload();

        var result = FeatureContextResponseBudget.Apply(payload, 8_192);

        Assert.NotEqual(true, result.IsError);
        var projected = ReadPayload(result);
        Assert.NotNull(projected.Declaration);
        Assert.NotNull(projected.Metrics);
        Assert.NotNull(projected.Callers);
        Assert.NotNull(projected.Tests);
        Assert.NotNull(projected.Violations);
        Assert.Contains(projected.Callers!.CallSites, call => call.ProjectName == "Production");
        Assert.Contains(projected.Tests!.TestFiles, file => file.EvidenceKind == "directInvocation");
        Assert.Equal(payload.Callers!.TotalCallers, projected.Callers.TotalCallers);
        Assert.Equal(payload.Tests!.TotalMatchingTests, projected.Tests.TotalMatchingTests);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.All(projected.Callers.CallSites, call => Assert.Contains(call.FilePath, text, StringComparison.Ordinal));
        Assert.Equal("truncated", projected.Completeness);
        Assert.Contains("responseBudget", projected.Callers.TruncatedBy ?? [], StringComparer.Ordinal);
    }

    [Fact]
    public void Apply_TooSmallBudgetReturnsErrorMinimumResult()
    {
        var result = FeatureContextResponseBudget.Apply(CreatePayload(), 512);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxResponseBytes", text, StringComparison.Ordinal);
        Assert.Contains("Mindestwert", text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", result.StructuredContent.Value.GetProperty("code").GetString());
        Assert.Equal(512, result.StructuredContent.Value.GetProperty("requestedBytes").GetInt32());
        Assert.True(result.StructuredContent.Value.GetProperty("minimumResponseBytes").GetInt32() > 512);
    }

    [Fact]
    public void Apply_LargerBudgetAddsCompleteCallerAndTestUnitsMonotonically()
    {
        var payload = CreatePayload(20);

        var small = FeatureContextResponseBudget.Apply(payload, 5_500);
        var large = FeatureContextResponseBudget.Apply(payload, 12_000);

        var smallPayload = ReadPayload(small);
        var largePayload = ReadPayload(large);
        Assert.True(CountVisibleCallers(smallPayload) <= CountVisibleCallers(largePayload));
        Assert.True(CountVisibleTests(smallPayload) <= CountVisibleTests(largePayload));
        Assert.Contains("Production", Assert.IsType<TextContentBlock>(Assert.Single(small.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("directInvocation", Assert.IsType<TextContentBlock>(Assert.Single(small.Content)).Text, StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(small).TotalBytes <= 5_500);
        Assert.True(McpResponseSize.From(large).TotalBytes <= 12_000);
    }

    [Fact]
    public void Apply_MinimumProjectionRanksDirectTestEvidenceBeforeAlphabeticalFileOrder()
    {
        var payload = CreatePayload(20);
        var direct = new StaticTestCandidateFileDto(
            "tests/ZDirectTests.cs", "ZDirectTests", "Unit", "methodInvocation", ["Run_Direct"], 1, 1,
            "directInvocation", "high", 1, ["ZDirectTests"]);
        var weaker = new StaticTestCandidateFileDto(
            "tests/AConventionTests.cs", "AConventionTests", "Unit", "namingConvention", [], 1, 0,
            "typeNamingConvention", "low", 1, ["AConventionTests"]);
        payload = payload with
        {
            Tests = payload.Tests! with
            {
                TotalMatchingTests = 2,
                TotalTestFiles = 2,
                TestFiles = [weaker, direct],
                DisplayedTestMethods = 1,
            },
        };

        var projected = ReadPayload(FeatureContextResponseBudget.Apply(payload, 5_500));

        Assert.Single(projected.Tests!.TestFiles);
        Assert.Equal("directInvocation", projected.Tests.TestFiles[0].EvidenceKind);
    }

    [Fact]
    public void ApplyFinal_PreservesNavigationAndReprojectsTextAndStructuredContentTogether()
    {
        var payload = CreatePayload(20);
        var navigation = new JsonObjectBuilder().BuildNavigation();
        var structured = JsonSerializer.SerializeToElement(
            new Dictionary<string, object?>
            {
                ["declaration"] = payload.Declaration,
                ["metrics"] = payload.Metrics,
                ["impact"] = payload.Callers,
                ["testContext"] = payload.Tests,
                ["violations"] = payload.Violations,
                ["completeness"] = payload.Completeness,
                ["navigation"] = navigation,
            },
            McpJsonOptions.Default);
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = FeatureContextFormatter.FormatReport(payload) + "\n## Navigation\n- status: operation=`ok`" }],
            StructuredContent = structured,
        };

        var projected = FeatureContextResponseBudget.ApplyFinal(result, 8_192);

        Assert.NotNull(projected.StructuredContent);
        Assert.True(projected.StructuredContent.Value.TryGetProperty("navigation", out _));
        Assert.Contains("## Navigation", Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text, StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 8_192);
    }

    private static FeatureContextPayload CreatePayload(int extraEntries = 8)
    {
        var callers = Enumerable.Range(0, extraEntries)
            .Select(index => new CallSiteEntry(
                $"tests/Caller{index:00}.cs",
                index + 1,
                "HotSymbol.Run",
                index == 0 ? "Production" : "Tests",
                $"Caller{index:00}.Invoke"))
            .Prepend(new CallSiteEntry("src/ProductionCaller.cs", 12, "HotSymbol.Run", "Production", "ProductionCaller.Invoke"))
            .ToList();
        var testFiles = Enumerable.Range(0, extraEntries)
            .Select(index => new StaticTestCandidateFileDto(
                $"tests/HotSymbolTests{index:00}.cs",
                $"HotSymbolTests{index:00}",
                "Unit",
                "methodInvocation",
                [$"Run_Case{index:00}"],
                1,
                1,
                "directInvocation",
                "high",
                1,
                [$"HotSymbolTests{index:00}"]))
            .Prepend(new StaticTestCandidateFileDto(
                "tests/HotSymbolDirectTests.cs",
                "HotSymbolDirectTests",
                "Unit",
                "methodInvocation",
                ["Run_Direct"],
                1,
                1,
                "directInvocation",
                "high",
                1,
                ["HotSymbolDirectTests"]))
            .ToList();

        var declaration = new SymbolDeclarationDto(
            "Demo.HotSymbol.Run", "Method", "public", "src/HotSymbol.cs", 10, 140, 131,
            "HotSymbol", "void", ["string input"], "M:Demo.HotSymbol.Run");
        var metrics = new MetricsLookupResultDto(
            declaration.Name,
            declaration.Kind,
            declaration.Name,
            declaration.DocCommentId,
            new SymbolLocationDto(declaration.FilePath, declaration.StartLine, declaration.EndLine),
            new MethodMetricsDto(131, 18, 24, 1, 1, []),
            null,
            null,
            Enumerable.Range(0, 12)
                .Select(index => new ThresholdCheckDto($"Metric{index:00}", index + 1, 5, "VIOLATION", $"ANL{index:0000}"))
                .ToList());
        var violations = Enumerable.Range(0, extraEntries)
            .Select(index => new ViolationItemDto($"ANL{index:0000}", new string('x', 220), index + 10, index == 0))
            .ToList();

        return new FeatureContextPayload(
            declaration,
            metrics,
            new CallersReportDto(callers.Count, callers, false),
            new StaticTestContextReportDto(testFiles.Count, testFiles.Count, testFiles, false),
            new ViolationsReportDto(violations.Count, 1, violations, false));
    }

    private static FeatureContextPayload ReadPayload(CallToolResult result) =>
        JsonSerializer.Deserialize<FeatureContextPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;

    private static int CountVisibleCallers(FeatureContextPayload payload) => payload.Callers?.CallSites.Count ?? 0;

    private static int CountVisibleTests(FeatureContextPayload payload) =>
        payload.Tests?.TestFiles.Sum(file => file.TestMethods.Count) ?? 0;

    private sealed class JsonObjectBuilder
    {
        internal Dictionary<string, object?> BuildNavigation() => new()
        {
            ["contractVersion"] = 1,
            ["status"] = new { operation = "ok", completeness = "complete" },
            ["next"] = (object?)null,
        };
    }
}
