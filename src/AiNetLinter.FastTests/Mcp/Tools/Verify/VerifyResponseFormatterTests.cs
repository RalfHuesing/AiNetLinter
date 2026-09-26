#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Unit")]
public sealed class VerifyResponseFormatterTests
{
    [Fact]
    public void ApiSurfaceNotConfigured_ReturnsContentOnlyErrorWithCompleteProjectListAndNoGateResult()
    {
        var result = VerifyResponseFormatter.ApiSurfaceNotConfigured(
        [
            new DeadCodeApiSurfaceIssue("Alpha", "DeadCode.DefaultApiSurface", null),
            new DeadCodeApiSurfaceIssue("Zeta", "ProjectOverrides.Zeta.DeadCode.ApiSurface", "Closed_Solution"),
        ]);

        var text = GetText(result);
        Assert.True(result.IsError);
        Assert.Single(result.Content);
        Assert.Contains("verdict: error", text, StringComparison.Ordinal);
        Assert.Contains("code: DEAD_CODE_API_SURFACE_NOT_CONFIGURED", text, StringComparison.Ordinal);
        Assert.Contains("projects: [Alpha, Zeta]", text, StringComparison.Ordinal);
        Assert.Contains("invalid: ProjectOverrides.Zeta.DeadCode.ApiSurface=Closed_Solution", text, StringComparison.Ordinal);
        Assert.Contains("ProjectOverrides.<Muster>.DeadCode.ApiSurface", text, StringComparison.Ordinal);
        Assert.Contains("external_library", text, StringComparison.Ordinal);
        Assert.Contains("closed_solution", text, StringComparison.Ordinal);
        Assert.Contains("beim Nutzer erfragen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("score:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("violationCount:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("deadCode:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_LongGateEvidence_UsesDeterministicWholeEntriesWithinFixedUtf8Budget()
    {
        var firstReason = "Grund: " + new string('ü', VerifyTool.ResponseBudgetBytes / 4);
        var parameters = CreateParameters(
            CreateScore(
                CreateViolation("src/First.cs", 10, "FirstRule", firstReason),
                CreateViolation("src/Second.cs", 20, "SecondRule", "Grund: " + new string('ü', VerifyTool.ResponseBudgetBytes / 4))));

        var first = VerifyResponseFormatter.Success(parameters);
        var second = VerifyResponseFormatter.Success(parameters);

        var text = GetText(first);
        Assert.Equal(text, GetText(second));
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("evidence: returned=1/2; truncation=response_budget", text, StringComparison.Ordinal);
        Assert.Contains(firstReason, text, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Second.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_GateEvidenceExceedingFixedBudget_ReturnsIncompleteInsteadOfAnUnactionableFailure()
    {
        var parameters = CreateParameters(CreateScore(CreateViolation(
            "src/TooLarge.cs", 10, "TooLargeRule", "Grund: " + new string('ü', VerifyTool.ResponseBudgetBytes))));

        var result = VerifyResponseFormatter.Success(parameters);

        var text = GetText(result);
        Assert.Contains("verdict: incomplete", text, StringComparison.Ordinal);
        Assert.Contains("isGateResult: false", text, StringComparison.Ordinal);
        Assert.Contains("reason: gateevidenceincomplete", text, StringComparison.Ordinal);
        Assert.DoesNotContain("score:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("violationCount:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("verdict: failed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_CleanPass_RemainsCompactAndDoesNotClaimTruncation()
    {
        var result = VerifyResponseFormatter.Success(CreateParameters(CreateScore()));

        var text = GetText(result);
        Assert.Equal("verdict: pass\ncompleteness: complete\nscore: 10.0\nviolationCount: 0\nscope: solution", text);
        Assert.True(Encoding.UTF8.GetByteCount(text) < 128);
    }

    [Fact]
    public void Success_DeadCodeUsesOnlyACompactSummaryAndRetrievalHint()
    {
        var advisory = new VerifyAdvisoryProjection(
            1,
            [new VerifyEvidenceEntry(
                "advisory_candidate",
                "dead_code",
                "advisory",
                "src/Probe.cs",
                42,
                "Keine produktiven statischen Referenzen; 2 Testreferenz(en) gefunden.",
                "h:abc",
                RequiresAgentJudgment: true,
                EvidenceBoundary: "statisch",
                CounterIndicators: ["Reflection"])],
            "complete",
            new VerifyDeadCodeSummary("complete", 1, Coverage: new DeadCodeScanCoverage("solution", 0, 0, 0, "finished", true)));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));

        Assert.DoesNotContain("advisories: count=1", text, StringComparison.Ordinal);
        Assert.Contains("verdict: pass\ncompleteness: complete\nscore: 10.0\nviolationCount: 0", text, StringComparison.Ordinal);
        Assert.Contains("deadCode: status=complete; candidates=1; scanCompleteness=complete; requestedScope=solution; processedDocuments=0; stopReason=finished", text, StringComparison.Ordinal);
        Assert.Contains("deadCodeHint: get_verify_advisories(category=dead_code)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("deadCodeAdvisoryHint:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("- category=dead_code", text, StringComparison.Ordinal);
        Assert.DoesNotContain("test_only", text, StringComparison.Ordinal);
        Assert.DoesNotContain("confidence", text, StringComparison.Ordinal);
        Assert.DoesNotContain("next=review_now", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, "deadCodeHint:"));
        Assert.DoesNotContain("source:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("counterIndicators:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_EvidenceSummaryNamesGateViolationsAndAllAdvisoryCategories()
    {
        var advisory = new VerifyAdvisoryProjection(
            1,
            [
                new VerifyEvidenceEntry("advisory_candidate", "magic_value", "advisory", "src/Probe.cs", 20, "Literal prüfen.", "src/Probe.cs:20"),
            ],
            "complete",
            new VerifyDeadCodeSummary("complete", 1));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(
            CreateScore(CreateViolation("src/Probe.cs", 5, "ProbeRule", "Regelverstoß.")), advisory)));

        Assert.Contains("evidence: returned=2/2; truncation=none; population=gate_violations+all_advisories", text, StringComparison.Ordinal);
        Assert.Contains("deadCode: status=complete; candidates=1", text, StringComparison.Ordinal);
        Assert.Contains("category=magic_value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("- category=dead_code", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
    }

    [Fact]
    public void Success_DeadCodeDetailsStayOutOfTheCompactSummary()
    {
        const string razorReason = "Razor-Referenzen nicht entscheidbar: generiertes C# fehlt oder ist nicht auswertbar; Razor-Generierung/Projektladung gegenprüfen.";
        var advisory = new VerifyAdvisoryProjection(
            1,
            [new VerifyEvidenceEntry(
                "advisory_candidate",
                "dead_code",
                "advisory",
                "src/Foo.razor.cs",
                12,
                razorReason,
                "h:razor",
                RequiresAgentJudgment: true,
                EvidenceBoundary: "statisch",
                CounterIndicators: ["Razor-Generierung/Projektladung"])],
            "complete",
            new VerifyDeadCodeSummary("complete", 1));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));

        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.DoesNotContain("Razor", text, StringComparison.Ordinal);
        Assert.Contains("deadCode: status=complete; candidates=1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("- category=dead_code", text, StringComparison.Ordinal);
        Assert.Contains("verdict: pass\ncompleteness: complete\nscore: 10.0\nviolationCount: 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_DeadCodeCountsAndHintSurviveEvidenceTruncation()
    {
        var entries = Enumerable.Range(0, 20)
            .Select(index => new VerifyEvidenceEntry(
                "advisory_candidate",
                "dead_code",
                "advisory",
                $"src/Probe{index}.cs",
                index + 1,
                "Keine produktiven statischen Referenzen. " + new string('x', 300),
                $"h:{index}"))
            .ToList();
        var advisory = new VerifyAdvisoryProjection(
            0,
            entries,
            "complete",
            new VerifyDeadCodeSummary("complete", 25, Coverage: new DeadCodeScanCoverage("solution", 100, 0, 10, "finished", true)));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));
        var summary = text.Split('\n').Single(line => line.StartsWith("deadCode:", StringComparison.Ordinal));
        Assert.Contains("candidates=25", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("testOnly", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("undecidable", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("shown=", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("truncatedBy=", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("- category=dead_code", text, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence:", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, "deadCodeHint:"));
        Assert.Contains("deadCodeHint: get_verify_advisories(category=dead_code)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_ZeroObservedCandidatesKeepTheirStatusAndUseTheVerifySnapshotToken()
    {
        var advisory = new VerifyAdvisoryProjection(
            1,
            [new VerifyEvidenceEntry("advisory_candidate", "magic_value", "advisory", "src/Probe.cs", 20, "Literal prüfen.", "src/Probe.cs:20")],
            "complete",
            new VerifyDeadCodeSummary(
                "complete",
                0,
                Coverage: new DeadCodeScanCoverage("solution", 100, 0, 10, "finished", true),
                ContinuationToken: "snapshot-token"));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(
            CreateScore(CreateViolation("src/Probe.cs", 5, "ProbeRule", "Regelverstoß.")), advisory)));

        Assert.Contains("score: 7.0\nviolationCount: 1", text, StringComparison.Ordinal);
        Assert.Contains("evidence: returned=2/2; truncation=none; population=gate_violations+all_advisories", text, StringComparison.Ordinal);
        Assert.Contains("deadCode: status=complete; candidates=0; scanCompleteness=complete", text, StringComparison.Ordinal);
        Assert.Contains("deadCodeHint: get_verify_advisories(category=dead_code, continuationToken=snapshot-token)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("clean", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Success_ReportsPartialAndUnavailableDeadCodeWithoutCleanClaims()
    {
        var partial = new VerifyAdvisoryProjection(
            0,
            [],
            "partial",
            new VerifyDeadCodeSummary("partial", 0, Coverage: new DeadCodeScanCoverage("solution", 0, 0, 0, "budget", false)));
        var partialText = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), partial)));
        Assert.Contains("deadCode: status=partial; candidates=0; scanCompleteness=partial", partialText, StringComparison.Ordinal);
        Assert.Contains("stopReason=", partialText, StringComparison.Ordinal);
        Assert.Contains("deadCodeHint: get_verify_advisories(category=dead_code)", partialText, StringComparison.Ordinal);
        Assert.DoesNotContain("undecidable", partialText, StringComparison.Ordinal);

        var unavailable = new VerifyAdvisoryProjection(
            0,
            [],
            "unavailable",
            new VerifyDeadCodeSummary("unavailable", null, "IOException"));
        var unavailableText = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), unavailable)));
        Assert.Contains("deadCode: status=unavailable; candidates=unknown; cause=IOException", unavailableText, StringComparison.Ordinal);
        Assert.Contains("cause=IOException", unavailableText, StringComparison.Ordinal);
        Assert.Contains("deadCodeHint: get_verify_advisories(category=dead_code)", unavailableText, StringComparison.Ordinal);
        Assert.Contains("verdict: pass", unavailableText, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_OversizedText_UsesTheFixedUtf8BudgetAndPreservesErrorSemantics()
    {
        var result = VerifyResponseFormatter.Error(
            "TEST_ERROR",
            new string('ü', VerifyTool.ResponseBudgetBytes),
            "Erneut ausführen.");

        var text = GetText(result);
        Assert.True(result.IsError == true);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("verdict: error", text, StringComparison.Ordinal);
        Assert.Contains("RESPONSE_BUDGET_EXCEEDED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_FormatsStandardErrorEnvelopeWithNavigationStatus()
    {
        var result = VerifyResponseFormatter.Error(
            "INVALID_ARGUMENT",
            "targetPath ist erforderlich.",
            "targetPath angeben.",
            "$.targetPath");

        var text = GetText(result);
        Assert.True(result.IsError == true);
        Assert.Contains("verdict: error", text, StringComparison.Ordinal);
        Assert.Contains("code: INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("field: $.targetPath", text, StringComparison.Ordinal);
        Assert.Contains("Status: operation=error, completeness=not_applicable", text, StringComparison.Ordinal);
        Assert.Contains("Aktion: targetPath angeben.", text, StringComparison.Ordinal);
    }

    private static VerifySuccessParameters CreateParameters(
        ScoreResult score,
        VerifyAdvisoryProjection? advisory = null) => new(
        VerifyScope.Solution,
        new VerifyScopeProjection(VerifyScope.Solution, VerifyScope.Solution, ["solution"], []),
        score,
        [],
        advisory ?? VerifyAdvisoryProjection.Empty);

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

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
