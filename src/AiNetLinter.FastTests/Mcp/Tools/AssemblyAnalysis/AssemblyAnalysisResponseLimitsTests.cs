#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis.Responses;
using AiNetLinter.Mcp.Validation;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblyAnalysisResponseLimitsTests
{
    [Fact]
    public void MinimumAssemblyBudget_ReturnsExecutableBudgetError()
    {
        var result = AssemblyAnalysisResponse.ValidateResponseBudget(200);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxResponseBytes", text, StringComparison.Ordinal);
        Assert.Contains("requestedBytes: 200", text, StringComparison.Ordinal);
        Assert.Contains($"minimumResponseBytes: {AssemblyAnalysisResponseLimits.MinimumResponseBytes}", text, StringComparison.Ordinal);
        Assert.Contains($"maxResponseBytes={AssemblyAnalysisResponseLimits.MinimumResponseBytes}", text, StringComparison.Ordinal);
        Assert.Null(AssemblyAnalysisResponse.ValidateResponseBudget(AssemblyAnalysisResponseLimits.MinimumResponseBytes));
    }

    [Fact]
    public void MinimumAssemblyBudget_WithNavigation_IncludesStatusLine()
    {
        var budgetError = AssemblyAnalysisResponse.ValidateResponseBudget(500);
        Assert.NotNull(budgetError);

        var navigated = AiNetLinter.Mcp.McpToolResults.WithNavigation(budgetError!, "C:\\virtual\\fixture.dll");
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(navigated.Content)).Text;

        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, StringComparison.Ordinal);
        Assert.Contains("Status: operation=error, completeness=not_applicable", text, StringComparison.Ordinal);
        Assert.Contains($"Aktion: denselben Aufruf mit maxResponseBytes={AssemblyAnalysisResponseLimits.MinimumResponseBytes} wiederholen.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AssemblyDetailLevelContract_UsesCanonicalValuesForToolsAndValidator()
    {
        Assert.Equal(["compact", "standard", "full"], McpEnumValues.AssemblyDetailLevels);
        Assert.Null(AssemblyAnalysisResponseLimits.ValidateDetailLevel(McpEnumValues.AssemblyDetailLevelCompact));
        Assert.Null(AssemblyAnalysisResponseLimits.ValidateDetailLevel(McpEnumValues.AssemblyDetailLevelStandard));
        Assert.Null(AssemblyAnalysisResponseLimits.ValidateDetailLevel(McpEnumValues.AssemblyDetailLevelFull));
        Assert.NotNull(AssemblyAnalysisResponseLimits.ValidateDetailLevel("verbose"));
    }

    [Fact]
    public void DiagnosticsProjection_DeduplicatesAfterDisplayTruncation()
    {
        var sharedPrefix = $"long diagnostic: {new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters * 2)}";
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            [sharedPrefix + " root detail"],
            [sharedPrefix + " transitive detail"]);

        Assert.Equal(2, summary.TotalCount);
        Assert.Single(summary.Samples);
        Assert.Single(summary.Root.Samples);
        Assert.Empty(summary.Transitive.Samples);
        Assert.Equal(summary.Samples.Count, summary.Samples.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(summary.ShownCount, summary.Root.ShownCount + summary.Transitive.ShownCount);
    }

    [Fact]
    public void DiagnosticsProjection_UsesOneGlobalSampleBudget()
    {
        var diagnostics = Enumerable.Range(0, AssemblyAnalysisResponseLimits.MaxDiagnostics * 2)
            .Select(index => $"diagnostic-{index:D3}: {new string('x', AssemblyAnalysisResponseLimits.MaxDiagnosticCharacters * 2)}")
            .ToArray();

        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            diagnostics.Take(AssemblyAnalysisResponseLimits.MaxDiagnostics),
            diagnostics.Skip(AssemblyAnalysisResponseLimits.MaxDiagnostics));

        Assert.Equal(diagnostics.Length, summary.TotalCount);
        Assert.Equal(summary.Samples.Count, summary.ShownCount);
        Assert.True(summary.ShownCount <= summary.TotalCount);
        Assert.Equal(summary.Samples.Count, summary.Samples.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(summary.ShownCount, summary.Root.ShownCount + summary.Transitive.ShownCount);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(string.Join("\n", summary.Samples))
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(string.Join("\n", summary.Root.Samples.Concat(summary.Transitive.Samples)))
            <= AssemblyAnalysisResponseLimits.MaxDiagnosticBytes);
    }

    [Fact]
    public void DiagnosticsProjection_TruncatedBy_DoesNotIncludeMaxDiagnosticBytesWhenOnlySlotLimitHit()
    {
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            ["root-1", "root-2", "root-3"],
            ["trans-1", "trans-2", "trans-3"],
            requestedLimit: 2);

        Assert.Equal(6, summary.TotalCount);
        Assert.Equal(2, summary.ShownCount);
        Assert.True(summary.Truncated);
        Assert.Contains("maxDiagnostics", summary.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.Root.TruncatedBy);
        Assert.DoesNotContain("maxDiagnosticBytes", summary.Transitive.TruncatedBy);
    }
}
