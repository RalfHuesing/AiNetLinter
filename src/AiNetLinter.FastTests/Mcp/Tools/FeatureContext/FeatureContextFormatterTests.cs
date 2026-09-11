#nullable enable

using AiNetLinter.Mcp.Tools.FeatureContext;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

[Trait("Category", "Unit")]
public sealed class FeatureContextFormatterTests
{
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
