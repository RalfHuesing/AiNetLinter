#nullable enable

using System;
using AiNetLinter.Mcp.Tools.FeatureContext;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FeatureContext;

public sealed partial class GetFeatureContextToolTests
{
    [Fact]
    public void ResolveCompleteness_SectionFailureMapsRootToPartialWithCauseAndNextStep()
    {
        var declaration = new SymbolDeclarationDto(
            "Broken", "Method", "public", "Broken.cs", 1, 1, 1, null, "void", [], null);
        var violations = new ViolationsReportDto(
            0,
            0,
            [],
            false,
            FeatureContextStatus.Error,
            FeatureContextReasonCodes.ViolationsScanFailed,
            [],
            "Abschnitt violations: den Lint-Abschnitt erneut anfordern und den Workspace-Fehler prüfen.");

        var completeness = FeatureContextScanner.ResolveCompleteness(
            FeatureContextStatus.Complete,
            callers: null,
            tests: null,
            violations: violations);
        var payload = new FeatureContextPayload(
            declaration,
            null,
            null,
            null,
            violations,
            Completeness: completeness,
            NextStep: violations.NextStep);
        var text = FeatureContextFormatter.FormatReport(payload);

        Assert.Equal(FeatureContextStatus.Partial, completeness);
        Assert.Contains("**Composite-Completeness:** `partial`", text, StringComparison.Ordinal);
        Assert.Contains("ReasonCode: `violations-scan-failed`", text, StringComparison.Ordinal);
        Assert.Contains("Workspace-Fehler prüfen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("**Composite-Completeness:** `error`", text, StringComparison.Ordinal);
    }
}
