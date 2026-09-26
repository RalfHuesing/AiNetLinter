#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Verify;

[Trait("Category", "Component")]
public sealed class VerifyAdvisoryPageStoreTests
{
    [Fact]
    public void ReusesOnlyCompleteMatchingSolutionSnapshots()
    {
        var scan = CandidateScan("complete");
        var pages = new VerifyAdvisoryPageStore();
        var context = new VerifyAdvisorySnapshotContext(VersionStamp.Create(), new object(), "solution");
        _ = pages.Start(scan, context);

        var matching = Assert.IsType<TextContentBlock>(Assert.Single(pages.TryReuse(context)!.Content)).Text;

        Assert.Contains("status=complete", matching, System.StringComparison.Ordinal);
        Assert.Null(pages.TryReuse(context with { SolutionVersion = VersionStamp.Create() }));
        Assert.Null(pages.TryReuse(context with { ConfigIdentity = new object() }));
        Assert.Null(pages.TryReuse(context with { Scope = "changes" }));
    }

    [Fact]
    public void DoesNotReusePartialSnapshotWithNoCandidates()
    {
        var pages = new VerifyAdvisoryPageStore();
        var context = new VerifyAdvisorySnapshotContext(VersionStamp.Create(), new object(), "solution");
        _ = pages.Start(CandidateScan("partial", includeCandidate: false), context);

        Assert.Null(pages.TryReuse(context));
    }

    [Fact]
    public void VerifyTokenOpensCapturedSnapshot()
    {
        var pages = new VerifyAdvisoryPageStore();
        var token = pages.Capture(CandidateScan("complete"),
            new VerifyAdvisorySnapshotContext(VersionStamp.Create(), new object(), "changes"));

        var result = pages.Continue(token);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Contains("Service.Unused", text, System.StringComparison.Ordinal);
        Assert.Contains("scanCompleteness=complete", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void PartialEmptyScanKeepsScanAndListCompletenessSeparate()
    {
        var result = GetVerifyAdvisoriesTool.Render(CandidateScan("partial", includeCandidate: false));
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Contains("status=partial; scope=unknown; candidates=0", text, System.StringComparison.Ordinal);
        Assert.Contains("scanCompleteness=partial", text, System.StringComparison.Ordinal);
        Assert.Contains("listCompleteness=complete", text, System.StringComparison.Ordinal);
    }

    private static DeadCodeScanResult CandidateScan(string status, bool includeCandidate = true)
    {
        DeadCodeEntry candidate = new(
            Id: "unused", Kind: "method", ContainerType: "Service", SymbolName: "Unused",
            File: "Service.cs", Line: 3, Column: 1, Accessibility: "private",
            Reason: "static scan", LimitsApplies: [], ProjectName: "TestApp",
            InternalSymbolIdentifier: "M:TestApp.Service.Unused");
        return new DeadCodeScanResult(
            includeCandidate ? [candidate] : [],
            new DeadCodeSummary(1, 1, includeCandidate ? 1 : 0,
                new Dictionary<string, int>(), Status: status),
            [], new DeadCodeRecommendedNextAction("countercheck", "prüfen"), IsTruncated: false);
    }
}
