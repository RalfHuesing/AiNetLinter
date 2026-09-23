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
        Assert.Contains("evidence: returned=1/2; truncation=response_budget", text, StringComparison.Ordinal);
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
    public void Success_AdvisoryUsesOneDirectReferenceWithoutRepeatedUncertaintyMetadata()
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
                Confidence: "low",
                EvidenceBoundary: "statisch",
                CounterIndicators: ["Reflection"],
                Usage: "test_only",
                TestReferences: 2)],
            "complete",
            new VerifyDeadCodeSummary("complete", 1, 1, 0, 0, 0));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));

        Assert.Contains("advisories: count=1; completeness=complete; review_required; static_evidence", text, StringComparison.Ordinal);
        Assert.Contains("verdict: pass\ncompleteness: complete\nscore: 10.0\nviolationCount: 0", text, StringComparison.Ordinal);
        Assert.Contains("deadCode: status=complete; candidates=1; testOnly=1; unreferenced=0; apiProtected=0; undecidable=0; shown=1; truncatedBy=0; next=review_now", text, StringComparison.Ordinal);
        Assert.Contains("deadCodeHint: Statischer Kandidat; Fehlalarm möglich. Vor Entfernen gegenprüfen.", text, StringComparison.Ordinal);
        Assert.Contains("- category=dead_code; symbolIdentifier=h:abc; ref=src/Probe.cs:42; usage=test_only; confidence=low; reason=no_production_static_reference; testReferences=2; countercheck=reflection,DI,generators,dynamic,markup/config,external_consumers", text, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(text, "deadCodeHint:"));
        Assert.DoesNotContain("source:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("counterIndicators:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Success_RazorAdvisoryReasonRemainsVisibleWithinTheFixedBudgetWithoutChangingVerdict()
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
                Confidence: "low",
                EvidenceBoundary: "statisch",
                CounterIndicators: ["Razor-Generierung/Projektladung"])],
            "complete",
            new VerifyDeadCodeSummary("complete", 1, 0, 1, 0, 0));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));

        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("razorEvidence=unavailable", text, StringComparison.Ordinal);
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
                "Keine produktiven statischen Referenzen.",
                $"h:{index}",
                Confidence: "high",
                Usage: index < 2 ? "test_only" : "unreferenced",
                TestReferences: index < 2 ? 1 : 0))
            .ToList();
        var advisory = new VerifyAdvisoryProjection(
            25,
            entries,
            "complete",
            new VerifyDeadCodeSummary("complete", 25, 2, 23, 1, 0));

        var text = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), advisory)));
        var summary = text.Split('\n').Single(line => line.StartsWith("deadCode:", StringComparison.Ordinal));
        var shown = int.Parse(summary.Split(';').Single(value => value.TrimStart().StartsWith("shown=", StringComparison.Ordinal)).Split('=')[1]);
        var truncatedBy = int.Parse(summary.Split(';').Single(value => value.TrimStart().StartsWith("truncatedBy=", StringComparison.Ordinal)).Split('=')[1]);

        Assert.Contains("candidates=25; testOnly=2; unreferenced=23; apiProtected=1; undecidable=0", summary, StringComparison.Ordinal);
        Assert.Equal(25, shown + truncatedBy);
        Assert.True(shown < entries.Count);
        Assert.Equal(1, CountOccurrences(text, "deadCodeHint:"));
    }

    [Fact]
    public void Success_ReportsPartialAndUnavailableDeadCodeWithoutCleanClaims()
    {
        var partial = new VerifyAdvisoryProjection(
            0,
            [],
            "partial",
            new VerifyDeadCodeSummary("partial", 0, 0, 0, 0, 3));
        var partialText = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), partial)));
        Assert.Contains("deadCode: status=partial; candidates=0; testOnly=0; unreferenced=0; apiProtected=0; undecidable=3; shown=0; truncatedBy=0; next=none", partialText, StringComparison.Ordinal);
        Assert.DoesNotContain("deadCodeHint:", partialText, StringComparison.Ordinal);

        var unavailable = new VerifyAdvisoryProjection(
            0,
            [],
            "unavailable",
            new VerifyDeadCodeSummary("unavailable", null, null, null, null, null, "IOException"));
        var unavailableText = GetText(VerifyResponseFormatter.Success(CreateParameters(CreateScore(), unavailable)));
        Assert.Contains("deadCode: status=unavailable; candidates=unknown; testOnly=unknown; unreferenced=unknown; apiProtected=unknown; undecidable=unknown; shown=0; truncatedBy=unknown; next=none", unavailableText, StringComparison.Ordinal);
        Assert.Contains("deadCodeCause: IOException", unavailableText, StringComparison.Ordinal);
        Assert.DoesNotContain("deadCodeHint:", unavailableText, StringComparison.Ordinal);
        Assert.Contains("verdict: pass", unavailableText, StringComparison.Ordinal);
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
