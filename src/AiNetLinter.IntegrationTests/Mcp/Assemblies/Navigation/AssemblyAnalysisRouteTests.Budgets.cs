#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

public sealed partial class AssemblyAnalysisRouteTests
{
    [Fact]
    public async Task AssemblyRoute_FindSymbolBatchPreservesEarlierPatternTruncation()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-batch-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "BatchProbe", "namespace Probe; public sealed class MatchOne { } public sealed class MatchTwo { } public sealed class MatchThree { } public sealed class Unique { }");
        await using var registry = new AssemblyAnalysisRegistry();
        var result = await AnalysisToolCall.ExecuteRouted(AssemblyAnalysisDispatcher.CreateRoute(registry), new AnalysisToolCallRequest(
            new AnalysisTargetRequest(assemblyPath), new AnalysisToolDispatch(
                AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(lease, new AssemblyFindSymbolRequest(["Match", "Unique"], null, 1, true), CancellationToken.None),
                ExpandAssemblyReferences: true), CancellationToken.None));
        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");
        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.False(navigation.GetProperty("assembliesTruncated").GetBoolean());
        Assert.True(navigation.GetProperty("resultsTruncated").GetBoolean());
        Assert.False(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.False(payload.GetProperty("wireBudget").GetProperty("truncated").GetBoolean());
        Assert.Equal(["maxResults"], payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(navigation.GetProperty("diagnostics").EnumerateArray().Select(item => item.GetString()!).Any(diagnostic => diagnostic.Contains("Treffer", StringComparison.Ordinal)), navigation.GetRawText());
    }

    [Fact]
    public async Task AssemblyRoute_WireBudgetMarksOnlyActualWireTruncation()
    {
        using var temp = TestTempDirectory.Create("assembly-route-symbol-wire-budget-");
        var declarations = Enumerable.Range(0, 120).Select(index => $"public sealed class Type{index:D3} {{ public int Value{index:D3} => {index}; }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "WireBudgetProbe", $"namespace Probe.Budget; {string.Join(Environment.NewLine, declarations)}");
        await using var registry = new AssemblyAnalysisRegistry();
        var result = await AnalysisToolCall.ExecuteRouted(AssemblyAnalysisDispatcher.CreateRoute(registry), new AnalysisToolCallRequest(
            new AnalysisTargetRequest(assemblyPath), new AnalysisToolDispatch(
                AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(lease, new AssemblyFindSymbolRequest(["Type"], null, 500, false), CancellationToken.None),
                MaxResponseBytes: 4096), CancellationToken.None));
        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var wireBudget = payload.GetProperty("wireBudget");
        Assert.True(wireBudget.GetProperty("truncated").GetBoolean(), payload.GetRawText());
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean(), payload.GetRawText());
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(wireBudget.GetProperty("totalBytes").GetInt32() <= wireBudget.GetProperty("limitBytes").GetInt32(), payload.GetRawText());
    }
}
