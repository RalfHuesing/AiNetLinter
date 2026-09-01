#nullable enable

using System;
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
}
