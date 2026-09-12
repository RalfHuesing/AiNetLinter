#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

[Trait("Category", "Component")]
public sealed class TestContextResponseBudgetTests
{
    [Fact]
    public void Apply_BudgetProjectionProtectsThreeConcreteCandidatesBeforeConventionEvidence()
    {
        var payload = CreatePayload();

        var result = TestContextResponseBudget.Apply(payload, 3_500);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("tests/ZDirect1Tests.cs", text, System.StringComparison.Ordinal);
        Assert.Contains("tests/ZDirect2Tests.cs", text, System.StringComparison.Ordinal);
        Assert.Contains("tests/ZDirect3Tests.cs", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("responseBudget", text, System.StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(result).TotalBytes <= 3_500);
    }

    [Fact]
    public void Apply_TooSmallForProtectedCandidatesReturnsErrorMinimumResult()
    {
        var result = TestContextResponseBudget.Apply(CreatePayload(), 512);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, System.StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxResponseBytes", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFinal_ReprojectsContentAfterNavigation()
    {
        var payload = CreatePayload();
        var report = TestContextFormatter.FormatReport(payload);
        var result = McpToolResults.Text(report + "\nStatus: operation=ok, completeness=complete");

        var projected = TestContextResponseBudget.ApplyFinal(result, 5_000);

        Assert.NotEqual(true, projected.IsError);
        Assert.Contains("Status: operation=ok", Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text, System.StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 5_000);
    }

    private static TestContextPayload CreatePayload()
    {
        var direct = Enumerable.Range(1, 3)
            .Select(index => new StaticTestCandidateFile(
                $"tests/ZDirect{index}Tests.cs", $"ZDirect{index}Tests", "Unit", "methodInvocation",
                [$"Run_{index}"], 1, "tests", "directInvocation", "high", 1, [$"ZDirect{index}Tests"]))
            .ToList();
        var convention = Enumerable.Range(1, 4)
            .Select(index => new StaticTestCandidateFile(
                $"tests/AConvention{index}Tests.cs", $"AConvention{index}Tests", "Unit", "namingConvention",
                [], 1, "tests", "typeNamingConvention", "low", 1, [$"AConvention{index}Tests"]));
        var files = convention.Concat(direct).ToList();
        return new TestContextPayload(
            "Demo.Target.Run", "Method", "src/Target.cs", 7, files.Count, files, [], false, false,
            ReturnedTestFiles: files.Count,
            ReturnedTestMethods: 3);
    }
}
