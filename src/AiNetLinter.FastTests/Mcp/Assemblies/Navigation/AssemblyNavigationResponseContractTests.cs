#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Component")]
// @covers AssemblyFindReferencesTool
// @covers TransitiveCallGraphFormatter
public sealed class AssemblyNavigationResponseContractTests
{
    [Fact]
    public async Task AssemblyFindReferences_NoHitKeepsNavigationDiagnosticsInTextAndStructuredContent()
    {
        using var temp = TestTempDirectory.Create("assembly-navigation-no-hit-");
        var dependencyPaths = Enumerable.Range(1, 6)
            .Select(index => AssemblyTestHelper.EmitAssembly(
                temp,
                $"NavigationMissingDependency{index}",
                $"namespace Probe; public sealed class DependencyType{index} {{ public int Value => {index}; }}"))
            .ToArray();
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "NavigationRoot",
            "namespace Probe; public sealed class Root { public int Read() => " +
            string.Join(" + ", Enumerable.Range(1, 6).Select(index => $"new DependencyType{index}().Value")) + "; }",
            dependencyPaths);
        foreach (var dependencyPath in dependencyPaths)
        {
            File.Delete(dependencyPath);
        }
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
        var completeness = payload.GetProperty("completeness");

        Assert.Contains("Keine Aufrufstellen gefunden", text, StringComparison.Ordinal);
        Assert.Contains("contentMode=decompiledProject", text, StringComparison.Ordinal);
        Assert.DoesNotContain("decompiledSignatureOnly", text, StringComparison.Ordinal);
        Assert.DoesNotContain("vollstaendig fuer den angefragten Scope", text, StringComparison.Ordinal);
        AssemblyNavigationResponseAssertions.AssertDiagnosticProjection(
            navigation,
            completeness,
            text,
            "NavigationMissingDependency",
            "Abhängigkeit nicht auflösbar: NavigationMissingDependency6");
    }

}

internal static class AssemblyNavigationResponseAssertions
{
    internal static void AssertDiagnosticProjection(
        JsonElement navigation,
        JsonElement? completeness,
        string text,
        string expectedSamplePrefix,
        string excludedSample)
    {
        var samples = navigation.GetProperty("diagnostics")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var totalCount = navigation.GetProperty("diagnosticTotalCount").GetInt32();
        var shownCount = navigation.GetProperty("diagnosticShownCount").GetInt32();
        var truncatedBy = navigation.GetProperty("diagnosticsTruncatedBy")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

        Assert.Equal("partial", navigation.GetProperty("completeness").GetString());
        Assert.Equal(5, samples.Length);
        Assert.Equal(5, shownCount);
        Assert.True(totalCount > shownCount, navigation.GetRawText());
        Assert.True(navigation.GetProperty("diagnosticsTruncated").GetBoolean());
        Assert.Equal(["maxDiagnostics"], truncatedBy);
        for (var index = 1; index <= 5; index++)
        {
            Assert.Contains(
                samples,
                sample => sample.Contains($"{expectedSamplePrefix}{index}", StringComparison.Ordinal));
        }
        Assert.DoesNotContain(
            samples,
            sample => sample.Contains($"{expectedSamplePrefix}6", StringComparison.Ordinal));
        Assert.DoesNotContain(excludedSample, string.Join("\n", samples), StringComparison.Ordinal);
        Assert.Contains(
            $"[{totalCount} Diagnosen gesamt, {shownCount} Samples gezeigt — gekürzt: maxDiagnostics]",
            text,
            StringComparison.Ordinal);
        Assert.All(
            samples,
            sample => Assert.Contains($"[Assembly-Diagnostic] {sample}", text, StringComparison.Ordinal));

        if (completeness is not { } projectedCompleteness) return;

        Assert.Equal(0, projectedCompleteness.GetProperty("totalCallSiteCount").GetInt32());
        Assert.Equal(0, projectedCompleteness.GetProperty("shownCallSiteCount").GetInt32());
        Assert.Equal(
            projectedCompleteness.GetProperty("diagnostics").GetRawText(),
            navigation.GetProperty("diagnostics").GetRawText());
        Assert.Equal(
            projectedCompleteness.GetProperty("diagnosticTotalCount").GetInt32(),
            totalCount);
        Assert.Equal(
            projectedCompleteness.GetProperty("diagnosticShownCount").GetInt32(),
            shownCount);
        Assert.Equal(
            projectedCompleteness.GetProperty("diagnosticsTruncated").GetBoolean(),
            navigation.GetProperty("diagnosticsTruncated").GetBoolean());
        Assert.Equal(
            projectedCompleteness.GetProperty("diagnosticsTruncatedBy").GetRawText(),
            navigation.GetProperty("diagnosticsTruncatedBy").GetRawText());
    }
}
