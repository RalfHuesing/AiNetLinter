#nullable enable

using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Unit")]
public sealed class AssemblyScopeFormatterTests
{
    [Fact]
    public void Format_SeparatesQueryScopeAndEmitsOnlySafeAssemblyIdentity()
    {
        var summary = new AssemblyNavigationSummary(
            IncludeReferences: true,
            TotalAssemblyCount: 2,
            SearchedAssemblyCount: 1,
            AssembliesTruncated: true,
            Completeness: "partial",
            Diagnostics: ["diagnostic"],
            DiagnosticTotalCount: 4,
            DiagnosticShownCount: 1,
            DiagnosticsTruncated: true,
            ResultsTruncated: true,
            RequestedIncludeReferences: true,
            EffectiveSearchMode: "bounded_reference_closure",
            ScopeIdentities:
            [
                new("Target.dll", "target-token", "content-token"),
            ]);

        var text = AssemblyScopeFormatter.Format(summary);

        Assert.Contains("requestedIncludeReferences=true", text, StringComparison.Ordinal);
        Assert.Contains("effectiveSearchMode=bounded_reference_closure", text, StringComparison.Ordinal);
        Assert.Contains("completeness=partial", text, StringComparison.Ordinal);
        Assert.Contains("leaseCompleteness=partial", text, StringComparison.Ordinal);
        Assert.Contains("assembliesSearched=1; assembliesTotal=2; assembliesTruncated=true", text, StringComparison.Ordinal);
        Assert.Contains("operationCompleteness=partial; resultsTruncated=true; diagnostics=1/4; diagnosticsTruncated=true", text, StringComparison.Ordinal);
        Assert.Contains("assemblyName=Target.dll; targetToken=target-token; contentToken=content-token", text, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAssemblyNavigation_UsesTheSharedScopeInsteadOfLegacyCompletenessFooter()
    {
        var result = GetSymbolBodyTool.AddAssemblyNavigation(
            McpToolResults.Text("body"),
            new AssemblyNavigationSummary(
                IncludeReferences: false,
                TotalAssemblyCount: 1,
                SearchedAssemblyCount: 1,
                AssembliesTruncated: false,
                Completeness: "complete",
                Diagnostics: [],
                ScopeIdentities: [new("Target.dll", "target-token", "content-token")]));
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Contains("Assembly-Scope:", text, StringComparison.Ordinal);
        Assert.Contains("requestedIncludeReferences=false", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly-Suche:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_UsesPartialCanonicalCompletenessWhenSnapshotIsPartial()
    {
        var result = AssemblyScopeFormatter.Append(
            McpToolResults.Text("[ASSEMBLY] snapshotCompleteness=partial\n\nbody"),
            new AssemblyNavigationSummary(false, 1, 1, false, "complete", []));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("completeness=partial", text, StringComparison.Ordinal);
        Assert.Contains("operationCompleteness=complete", text, StringComparison.Ordinal);
        Assert.Contains("leaseCompleteness=complete", text, StringComparison.Ordinal);
    }
}
