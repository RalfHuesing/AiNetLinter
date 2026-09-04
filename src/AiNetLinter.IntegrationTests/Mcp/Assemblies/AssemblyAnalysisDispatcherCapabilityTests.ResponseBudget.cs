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
    public async Task AssemblyRoute_MaxResponseBytesBelowMinimumReturnsRecoverableArgument()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-minimum-budget-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, []);

        var result = await fixture.ExecuteInspectAsync(maxResponseBytes: 1);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.False(result.IsError);
        Assert.Contains("maxResponseBytes", text, StringComparison.Ordinal);
        Assert.Contains(AssemblyAnalysisResponseLimits.MinimumResponseBytes.ToString(), text, StringComparison.Ordinal);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
    }

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
    public async Task AssemblyRoute_ExposesSourcePolicyInAnalysisEnvelopeAndHeader()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-source-policy-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourcePolicy: "decompilation_allowed");

        var result = await fixture.ExecuteInspectAsync();
        var payload = Structured(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Equal(
            "decompilation_allowed",
            payload.GetProperty("analysis").GetProperty("sourcePolicy").GetString());
        Assert.Contains("sourcePolicy=decompilation_allowed", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_ExposesDefaultSourcePolicyInAnalysisEnvelopeAndHeader()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-default-source-policy-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, []);

        var result = await fixture.ExecuteInspectAsync();
        var payload = Structured(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Equal(
            "source_preferred",
            payload.GetProperty("analysis").GetProperty("sourcePolicy").GetString());
        Assert.Contains("sourcePolicy=source_preferred", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_FinalWireTrimRecalculatesCountsAndCursorAt4096Bytes()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-final-wire-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Page{index:D3} {{ }}");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var first = Structured(await fixture.ExecuteInspectAsync(maxResponseBytes: 4096));
        var firstTypes = first.GetProperty("types").EnumerateArray().ToArray();
        var firstCount = first.GetProperty("returnedCount").GetInt32();
        var firstToken = first.GetProperty("continuationToken").GetString();

        Assert.NotEmpty(firstTypes);
        Assert.Equal(firstTypes.Length, firstCount);
        Assert.True(first.GetProperty("totalCount").GetInt32() > firstCount);
        Assert.Equal(firstCount.ToString(), firstToken);

        var second = Structured(await fixture.ExecuteInspectAsync(
            maxResponseBytes: 4096,
            cursor: firstToken));
        Assert.NotEmpty(second.GetProperty("types").EnumerateArray());
        Assert.DoesNotContain(
            second.GetProperty("types").EnumerateArray().Select(type => type.GetProperty("id").GetString()),
            id => firstTypes.Any(type => type.GetProperty("id").GetString() == id));
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
            CancellationToken.None),
            maxResponseBytes: 4096);

        var payload = Structured(result);
        var section = payload.GetProperty("body");
        var bodyResult = Assert.Single(section.GetProperty("results").EnumerateArray());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var textBytes = Encoding.UTF8.GetByteCount(text);
        var structuredBytes = JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

        Assert.True(textBytes + structuredBytes <= 4096);
        Assert.Equal(4096, payload.GetProperty("wireBudget").GetProperty("limitBytes").GetInt32());
        Assert.Equal(textBytes + structuredBytes, payload.GetProperty("wireBudget").GetProperty("totalBytes").GetInt32());
        Assert.Equal("truncated", bodyResult.GetProperty("body").GetProperty("status").GetString());
        Assert.True(bodyResult.GetProperty("body").GetProperty("truncated").GetBoolean());
        Assert.Contains("maxResponseBytes", bodyResult.GetProperty("body").GetProperty("detailHint").GetString(), StringComparison.Ordinal);
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
