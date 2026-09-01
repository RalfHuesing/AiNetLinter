#nullable enable

using System;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Unit")]
public sealed class TransitiveCallGraphFormatterTests
{
    [Fact]
    public void CreateDiagnosticProjection_UsesFiveSamplesAndReportsTotalCountAndTruncationReason()
    {
        var diagnostics = new[] { "diagnostic-1", "diagnostic-2", "diagnostic-3", "diagnostic-4", "diagnostic-5", "diagnostic-6" };

        var projection = TransitiveCallGraphFormatter.CreateDiagnosticProjection(diagnostics);

        Assert.Equal(6, projection.TotalCount);
        Assert.Equal(5, projection.Samples.Count);
        Assert.Equal(diagnostics[..5], projection.Samples);
        Assert.True(projection.Truncated);
        Assert.Equal(["maxDiagnostics"], projection.TruncatedBy);
    }

    [Fact]
    public void Format_UsesSameDiagnosticSamplesAndTruncationMetadata()
    {
        var result = new ReferenceTraversalResult(
            Array.Empty<TransitiveCallSiteEntry>(),
            new TraversalCompleteness(
                RequestedDepth: 1,
                EffectiveDepth: 1,
                VisitedNodeCount: 1,
                TotalCallSiteCount: 0,
                ShownCallSiteCount: 0,
                TruncatedByMaxResults: false,
                TruncatedByNodeLimit: false,
                DepthWasClamped: false,
                Diagnostics: ["diagnostic-1", "diagnostic-2", "diagnostic-3", "diagnostic-4", "diagnostic-5", "diagnostic-6"]));

        var text = TransitiveCallGraphFormatter.Format(result);

        Assert.Contains("[Assembly-Diagnostic] diagnostic-1", text, StringComparison.Ordinal);
        Assert.Contains("[Assembly-Diagnostic] diagnostic-5", text, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-6", text, StringComparison.Ordinal);
        Assert.Contains("[6 Diagnosen gesamt, 5 Samples gezeigt — gekürzt: maxDiagnostics]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatResponse_ProjectsSharedDiagnosticsOnce_AndKeepsNoHitMetadata()
    {
        var diagnostics = new[]
        {
            "diagnostic-1", "diagnostic-2", "diagnostic-3",
            "diagnostic-4", "diagnostic-5", "diagnostic-6",
        };
        var navigation = new AssemblyNavigationSummary(
            IncludeReferences: true,
            TotalAssemblyCount: 2,
            SearchedAssemblyCount: 2,
            AssembliesTruncated: false,
            Completeness: "partial",
            Diagnostics: diagnostics);
        var result = new ReferenceTraversalResult(
            Array.Empty<TransitiveCallSiteEntry>(),
            new TraversalCompleteness(
                RequestedDepth: 1,
                EffectiveDepth: 1,
                VisitedNodeCount: 1,
                TotalCallSiteCount: 0,
                ShownCallSiteCount: 0,
                TruncatedByMaxResults: false,
                TruncatedByNodeLimit: false,
                DepthWasClamped: false,
                Diagnostics: diagnostics),
            navigation);

        var formatted = TransitiveCallGraphFormatter.FormatResponse(
            result,
            "Keine Aufrufstellen gefunden");
        var completeness = formatted.Traversal.Completeness;
        var projectedNavigation = formatted.Traversal.Navigation!;

        Assert.Equal(6, completeness.DiagnosticTotalCount);
        Assert.Equal(5, completeness.DiagnosticShownCount);
        Assert.True(completeness.DiagnosticsTruncated);
        Assert.Equal(["maxDiagnostics"], completeness.DiagnosticsTruncatedBy);
        Assert.Equal(completeness.DiagnosticTotalCount, projectedNavigation.DiagnosticTotalCount);
        Assert.Equal(completeness.DiagnosticShownCount, projectedNavigation.DiagnosticShownCount);
        Assert.Equal(completeness.DiagnosticsTruncated, projectedNavigation.DiagnosticsTruncated);
        Assert.Equal(completeness.DiagnosticsTruncatedBy, projectedNavigation.DiagnosticsTruncatedBy);
        Assert.Equal(completeness.Diagnostics, projectedNavigation.Diagnostics);
        Assert.Contains("Keine Aufrufstellen gefunden", formatted.Text, StringComparison.Ordinal);
        Assert.Contains("[Assembly-Diagnostic] diagnostic-5", formatted.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-6", formatted.Text, StringComparison.Ordinal);
        Assert.Contains("[6 Diagnosen gesamt, 5 Samples gezeigt — gekürzt: maxDiagnostics]", formatted.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatAssemblyCallTreeResponse_UsesProjectedNavigationMetadataInTextAndPayload()
    {
        var diagnostics = new[]
        {
            "diagnostic-1", "diagnostic-2", "diagnostic-3",
            "diagnostic-4", "diagnostic-5", "diagnostic-6",
        };
        var result = TransitiveCallGraphFormatter.FormatAssemblyCallTreeResponse(
            new AssemblyCallTreeResponseRequest(
                new MetricsTreeNode("Root", string.Empty, 0, 0, "Root", []),
                "Root — Root",
                new AssemblyNavigationSummary(
                    IncludeReferences: true,
                    TotalAssemblyCount: 2,
                    SearchedAssemblyCount: 2,
                    AssembliesTruncated: false,
                    Completeness: "partial",
                    Diagnostics: diagnostics),
                diagnostics,
                Truncated: false,
                TopNTruncated: false,
                TreeTruncationMessage: null));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var payload = JsonSerializer.Deserialize<AssemblyCallTreeResult>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);

        Assert.NotNull(payload);
        Assert.Contains("[Assembly-Diagnostic] diagnostic-5", text, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic-6", text, StringComparison.Ordinal);
        Assert.Contains("[6 Diagnosen gesamt, 5 Samples gezeigt — gekürzt: maxDiagnostics]", text, StringComparison.Ordinal);
        Assert.Equal(6, payload!.Navigation.DiagnosticTotalCount);
        Assert.Equal(5, payload.Navigation.DiagnosticShownCount);
        Assert.True(payload.Navigation.DiagnosticsTruncated);
        Assert.Equal(["maxDiagnostics"], payload.Navigation.DiagnosticsTruncatedBy);
        Assert.Equal(diagnostics[..5], payload.Navigation.Diagnostics);
    }
}
