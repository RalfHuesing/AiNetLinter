#nullable enable

using System;
using System.Text;
using AiNetLinter.Mcp.Tools.Verify;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Unit")]
public sealed class VerifyResponseFormatterTests
{
    [Fact]
    public void Success_LongGateEvidence_UsesDeterministicWholeEntriesWithinFixedUtf8Budget()
    {
        var firstReason = "Grund: " + new string('ü', 1_000);
        var parameters = CreateParameters(
            CreateScore(
                CreateViolation("src/First.cs", 10, "FirstRule", firstReason),
                CreateViolation("src/Second.cs", 20, "SecondRule", "Grund: " + new string('ü', 1_000))));

        var first = VerifyResponseFormatter.Success(parameters);
        var second = VerifyResponseFormatter.Success(parameters);

        var text = GetText(first);
        Assert.Equal(text, GetText(second));
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("truncationReason: response_budget", text, StringComparison.Ordinal);
        Assert.Contains("gateTotalCount: 2", text, StringComparison.Ordinal);
        Assert.Contains("gateReturnedCount: 1", text, StringComparison.Ordinal);
        Assert.Contains(firstReason, text, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Second.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_GateEvidenceExceedingFixedBudget_ReturnsIncompleteInsteadOfAnUnactionableFailure()
    {
        var parameters = CreateParameters(CreateScore(CreateViolation(
            "src/TooLarge.cs", 10, "TooLargeRule", "Grund: " + new string('ü', 3_000))));

        var result = VerifyResponseFormatter.Success(parameters);

        var text = GetText(result);
        Assert.Contains("verdict: incomplete", text, StringComparison.Ordinal);
        Assert.Contains("decisionReason: gateevidenceincomplete", text, StringComparison.Ordinal);
        Assert.DoesNotContain("verdict: failed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_CleanPass_RemainsCompactAndDoesNotClaimTruncation()
    {
        var result = VerifyResponseFormatter.Success(CreateParameters(CreateScore()));

        var text = GetText(result);
        Assert.Contains("verdict: pass", text, StringComparison.Ordinal);
        Assert.Contains("truncationReason: none", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) < 1_024);
    }

    [Fact]
    public void Error_OversizedText_UsesTheFixedUtf8BudgetAndPreservesErrorSemantics()
    {
        var result = VerifyResponseFormatter.Error(
            "TEST_ERROR",
            new string('ü', 3_000),
            "Erneut ausführen.");

        var text = GetText(result);
        Assert.True(result.IsError == true);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("verdict: error", text, StringComparison.Ordinal);
        Assert.Contains("RESPONSE_BUDGET_EXCEEDED", text, StringComparison.Ordinal);
    }

    private static VerifySuccessParameters CreateParameters(ScoreResult score) => new(
        VerifyScope.Solution,
        new VerifyScopeProjection(VerifyScope.Solution, VerifyScope.Solution, ["solution"], []),
        score,
        [],
        VerifyAdvisoryProjection.Empty);

    private static ScoreResult CreateScore(params ViolationEntry[] violations) => new(
        Passed: violations.Length == 0,
        Score: violations.Length == 0 ? 10.0 : 7.0,
        Threshold: VerifyGateSummary.RequiredScore,
        Violations: violations,
        Remediation: new RemediationHint("Test", [], "Docs/linter/configuration.md"),
        Summary: "Test",
        Scope: "solution",
        ScoreIsNotScope: true,
        Completeness: "complete",
        Status: violations.Length == 0 ? "passed" : "failed",
        TotalViolationCount: violations.Length,
        ShownViolationCount: violations.Length);

    private static ViolationEntry CreateViolation(string path, int line, string rule, string details) => new(
        path, line, rule, details, "error", "Beheben");

    private static string GetText(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
