#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_BudgetsFinalEnrichedResponseThroughDispatcher()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-response-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Type{index:D3} {{ public string Value{index:D3} => \"value\"; public void Reset{index:D3}(string input) {{ }} }}");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var result = await fixture.ExecuteInspectAsync();
        var payload = Structured(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var structuredBytes = JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;
        var textBytes = Encoding.UTF8.GetByteCount(text);
        var wireBudget = payload.GetProperty("wireBudget");

        Assert.True(payload.GetProperty("totalTypes").GetInt32() > payload.GetProperty("shownCount").GetInt32());
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(textBytes + structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Equal(textBytes, wireBudget.GetProperty("textBytes").GetInt32());
        Assert.Equal(structuredBytes, wireBudget.GetProperty("structuredBytes").GetInt32());
        Assert.Equal(textBytes + structuredBytes, wireBudget.GetProperty("totalBytes").GetInt32());
        Assert.Equal(AssemblyAnalysisResponseLimits.DefaultResponseBytes, wireBudget.GetProperty("limitBytes").GetInt32());
        Assert.Equal("assembly", payload.GetProperty("analysis").GetProperty("targetType").GetString());
        Assert.Equal(payload.GetProperty("types").GetArrayLength(), payload.GetProperty("shownCount").GetInt32());
    }

    [Fact]
    public async Task AssemblyContext_BudgetKeepsSectionStatusAndDetailHint()
    {
        using var temp = TestTempDirectory.Create("assembly-context-section-budget-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: "namespace Probe; public static class Probe { public static int Run() => 42; }");

        var result = await fixture.ExecuteRootOnlyAsync(lease => AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                "Probe.Run",
                IncludeMetrics: true,
                IncludeReferences: false,
                IncludeCallers: true,
                IncludeImpact: true,
                IncludeBody: true,
                IncludeClassStructure: true,
                MaxResults: 100,
                MaxBodyLines: 80,
                MaxCallers: 100,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 4096,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None));

        var payload = Structured(result);
        var section = payload.GetProperty("body");
        Assert.Equal("truncated", section.GetProperty("status").GetString());
        Assert.True(section.GetProperty("truncated").GetBoolean());
        Assert.Contains("maxResponseBytes", section.GetProperty("detailHint").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyContext_TopNLimitsCallerAndImpactSelection()
    {
        using var temp = TestTempDirectory.Create("assembly-context-topn-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: "namespace Probe; public static class Probe { public static int Run() => 42; public static int First() => Run(); public static int Second() => Run(); }");

        var result = await fixture.ExecuteRootOnlyAsync(lease => AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                "Probe.Run",
                IncludeMetrics: false,
                IncludeReferences: false,
                IncludeCallers: true,
                IncludeImpact: true,
                IncludeBody: false,
                IncludeClassStructure: false,
                MaxResults: 100,
                MaxBodyLines: 80,
                MaxCallers: 100,
                Depth: 1,
                TopN: 1,
                MaxResponseBytes: 16 * 1024,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None));

        var payload = Structured(result);
        Assert.Equal(1, payload.GetProperty("callers").GetProperty("callSites").GetArrayLength());
        Assert.True(payload.GetProperty("impact").GetProperty("completeness").GetProperty("shownCallSiteCount").GetInt32() <= 1);
    }
}
