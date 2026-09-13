#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.TestKit;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
public sealed class AssemblyAnalysisContextTextModelTests
{
    [Fact]
    public async Task ExecuteWithoutSymbol_ProvidesAssemblyOverview()
    {
        using var temp = TestTempDirectory.Create("assembly-context-overview-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ContextOverviewProbe",
            "namespace Probe.Api; public sealed class PublicApi { }");
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var result = await AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                SymbolIdentifier: null,
                IncludeMetrics: false,
                IncludeReferences: true,
                IncludeCallers: false,
                IncludeImpact: false,
                IncludeBody: false,
                IncludeClassStructure: false,
                MaxResults: 10,
                MaxBodyLines: 80,
                MaxCallers: 10,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 2_048,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Abschnitt: assembly", text, StringComparison.Ordinal);
        Assert.Contains("Assembly: `ContextOverviewProbe`", text, StringComparison.Ordinal);
        Assert.Contains("Zielframework:", text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche Namespaces: 1", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Api`", text, StringComparison.Ordinal);
        Assert.Contains("Referenzen:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("API-Typen:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatContextOverview_ProvidesAssemblyDiscoveryWithoutTypeDump()
    {
        var payload = new InspectAssemblyPayload(
            "fixture.dll",
            new AssemblyIdentityDto("Probe.Library", "1.2.3.4", "neutral", "null"),
            ["Probe.Api"],
            [new AssemblyReferenceDto("Probe.Dependency", "2.0.0.0", "neutral", true)],
            [],
            [],
            "complete",
            false,
            1,
            0,
            []);

        var text = InspectAssemblyFormatter.FormatContextOverview(payload, ".NETCoreApp,Version=v10.0");

        Assert.Contains("Assembly: `Probe.Library`", text, StringComparison.Ordinal);
        Assert.Contains("Identität: Probe.Library, Version 1.2.3.4", text, StringComparison.Ordinal);
        Assert.Contains("Zielframework: `.NETCoreApp,Version=v10.0`", text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche Namespaces: 1", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Api`", text, StringComparison.Ordinal);
        Assert.Contains("Probe.Dependency, Version 2.0.0.0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("API-Typen:", text, StringComparison.Ordinal);
    }

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
