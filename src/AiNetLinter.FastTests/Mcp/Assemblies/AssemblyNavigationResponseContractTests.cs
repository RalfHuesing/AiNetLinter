#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
// @covers AssemblyFindReferencesTool
// @covers TransitiveCallGraphFormatter
public sealed class AssemblyNavigationResponseContractTests
{
    [Fact]
    public async Task AssemblyFindReferences_NoHitKeepsNavigationDiagnosticsInTextAndStructuredContent()
    {
        using var temp = TestTempDirectory.Create("assembly-navigation-no-hit-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "NavigationMissingDependency",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "NavigationRoot",
            "namespace Probe; public sealed class Root { public int Read() => 1; }",
            dependencyPath);
        File.Delete(dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest("Probe.Root.Read", 50, 1, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");
        var samples = navigation.GetProperty("diagnostics")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var totalCount = navigation.GetProperty("diagnosticTotalCount").GetInt32();
        var shownCount = navigation.GetProperty("diagnosticShownCount").GetInt32();

        Assert.Contains("Keine Aufrufstellen gefunden", text, StringComparison.Ordinal);
        Assert.True(totalCount > 0, navigation.GetRawText());
        Assert.Equal(samples.Length, shownCount);
        Assert.InRange(shownCount, 1, 5);
        Assert.True(totalCount >= shownCount);
        Assert.Equal(totalCount > shownCount, navigation.GetProperty("diagnosticsTruncated").GetBoolean());
        Assert.Contains($"[{totalCount} Diagnosen gesamt, {shownCount} Samples gezeigt", text, StringComparison.Ordinal);
        Assert.All(samples, sample => Assert.Contains(sample, text, StringComparison.Ordinal));
        if (totalCount > shownCount)
        {
            Assert.Equal(
                ["maxDiagnostics"],
                navigation.GetProperty("diagnosticsTruncatedBy").EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToArray());
        }
    }
}
