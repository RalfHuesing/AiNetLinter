#nullable enable

using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FeatureContext;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Unit")]
public sealed class FeatureContextFormatterTests
{
    [Fact]
    public void FormatReport_EmitsNoCanonicalIdAndOnlyOneCompositeNextStep()
    {
        var declaration = new SymbolDeclarationDto(
            "Probe.Run", "Method", "public", "Probe.cs", 1, 2, 2, null, "void", [], "M:Probe.Run");
        var callers = new CallersReportDto(0, [], false, NextStep: "Impact erneut anfordern.");
        var tests = new StaticTestContextReportDto(0, 0, [], false, NextStep: "Testkontext erneut anfordern.");
        var violations = new ViolationsReportDto(0, 0, [], false, NextStep: "Violations erneut anfordern.");
        var payload = new FeatureContextPayload(
            declaration, null, callers, tests, violations, NextStep: "Gesamtkontext erneut anfordern.");

        var text = FeatureContextFormatter.FormatReport(payload);

        Assert.DoesNotContain("M:Probe.Run", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("Nächster sicherer Schritt", StringSplitOptions.None).Length - 1);
        Assert.Contains("Gesamtkontext erneut anfordern.", text, StringComparison.Ordinal);
        var structured = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default);
        Assert.Equal("M:Probe.Run", structured.GetProperty("declaration").GetProperty("id").GetString());
        Assert.False(structured.GetProperty("declaration").TryGetProperty("docCommentId", out _));
        Assert.False(structured.GetProperty("impact").TryGetProperty("nextStep", out _));
        Assert.False(structured.GetProperty("testContext").TryGetProperty("nextStep", out _));
        Assert.False(structured.GetProperty("violations").TryGetProperty("nextStep", out _));
    }

    [Fact]
    public void FormatReport_DoesNotPresentUnavailableViolationsAsEmpty()
    {
        var declaration = new SymbolDeclarationDto(
            "Missing", "Method", "public", "Missing.cs", 1, 1, 1, null, "void", [], null);
        var payload = new FeatureContextPayload(
            declaration,
            null,
            null,
            null,
            new ViolationsReportDto(
                0, 0, [], false, FeatureContextStatus.NotDecidable,
                FeatureContextReasonCodes.SourceFileUnavailable));

        var text = FeatureContextFormatter.FormatReport(payload);

        Assert.Contains("Status: not_decidable", text, StringComparison.Ordinal);
        Assert.Contains("ReasonCode: `source-file-unavailable`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Linter-Verstoesse", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatReport_TypeConventionUsesClassCountInsteadOfMethodCount()
    {
        var declaration = new SymbolDeclarationDto(
            "CoreLib.Calculator", "NamedType", "public", "src/CoreLib/Calculator.cs", 1, 10, 10, null, null, [], null);
        var tests = new StaticTestContextReportDto(
            25,
            1,
            [new StaticTestCandidateFileDto(
                "tests/CoreLib.Tests/CalculatorTests.cs",
                "CalculatorTests",
                "Unit",
                TestCoverageMatchReasons.NamingConventionMatch,
                [],
                25,
                0,
                "typeNamingConvention",
                "low",
                25)],
            false,
            0);
        var text = FeatureContextFormatter.FormatReport(new FeatureContextPayload(
            declaration, null, null, tests, null));

        Assert.Contains("typeNamingConvention", text, StringComparison.Ordinal);
        Assert.Contains("confidence=low", text, StringComparison.Ordinal);
        Assert.Contains("25 Tests auf Klassenebene", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0 von 0", text, StringComparison.Ordinal);
    }
}
