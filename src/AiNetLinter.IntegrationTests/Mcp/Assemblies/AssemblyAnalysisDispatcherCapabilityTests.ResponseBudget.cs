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

        Assert.True(payload.GetProperty("totalTypes").GetInt32() > payload.GetProperty("shownCount").GetInt32());
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Equal("assembly", payload.GetProperty("analysis").GetProperty("targetType").GetString());
        Assert.Equal(payload.GetProperty("types").GetArrayLength(), payload.GetProperty("shownCount").GetInt32());
    }
}
