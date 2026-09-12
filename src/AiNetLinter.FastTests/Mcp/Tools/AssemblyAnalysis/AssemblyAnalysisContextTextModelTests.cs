#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblyAnalysisContextTextModelTests
{
    [Fact]
    public void Render_PreservesSectionTextAndContinuationFromTypedModel()
    {
        var model = new AssemblyAnalysisContextTextModel(
            TotalCount: 3,
            ReturnedCount: 1,
            IsTruncated: true,
            ContinuationToken: "next-page",
            Scope: "root",
            Completeness: "partial",
            SymbolIdentifier: "M:Probe.Type.Read",
            Sections: new Dictionary<string, string>
            {
                ["body"] = "handoffId: `a:owner:member`",
            });

        var text = AssemblyAnalysisContextTool.RenderText(model);

        Assert.Contains("Assembly-Kontext: 1 von 3", text, System.StringComparison.Ordinal);
        Assert.Contains("Abschnitt: body", text, System.StringComparison.Ordinal);
        Assert.Contains("handoffId: `a:owner:member`", text, System.StringComparison.Ordinal);
        Assert.Contains("continuationToken=next-page", text, System.StringComparison.Ordinal);
    }
}
