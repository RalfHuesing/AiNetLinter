#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class Group3ContextAndCallTreeHandoffTests
{
    private static string TextOf(CallToolResult result)
    {
        var block = Assert.Single(result.Content);
        var textBlock = Assert.IsType<TextContentBlock>(block);
        return textBlock.Text;
    }

    [Fact]
    public void CallGraphTextRenderer_ProducesOpaqueHandlesInAsciiAndMermaid()
    {
        var graph = new CallGraphPayload(
            "n1",
            [
                new CallGraphNode("n1", "i:0:SymbolGraphMini:SymbolGraphMini.Greeter.Greet#1", "Greet", "Greeter.cs:5", "method"),
                new CallGraphNode("n2", "i:0:SymbolGraphMini:SymbolGraphMini.Caller.Run#1", "Run", "Caller.cs:5", "method"),
            ],
            [new CallGraphEdge("n2", "n1", [new CallGraphCallSite("Caller.cs", 5, 1, "SymbolGraphMini")])]);

        // 1. ASCII output
        var ascii = CallGraphTextRenderer.RenderAscii(graph);
        var asciiMatch = Regex.Match(ascii, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`");
        Assert.True(asciiMatch.Success, $"Expected h:... handle in ASCII call tree: {ascii}");
        Assert.DoesNotContain("handoffId: `i:", ascii, StringComparison.Ordinal);

        // 2. Mermaid output
        var mermaid = CallGraphTextRenderer.RenderMermaid(graph);
        var mermaidMatch = Regex.Match(mermaid, @"%% handoffId:\s*n1\s*=\s*(?<handle>h:[a-zA-Z0-9]+)");
        Assert.True(mermaidMatch.Success, $"Expected h:... handle in Mermaid comments: {mermaid}");
        Assert.DoesNotContain("%% handoffId: n1 = i:", mermaid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCallTree_ConsumesOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        // Find symbol to get opaque handle
        var findResult = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greet"],
            kind: "method",
            maxResults: 10,
            CancellationToken.None);
        var findText = TextOf(findResult);
        var handle = Regex.Match(findText, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`").Groups["handle"].Value;
        Assert.NotEmpty(handle);

        var callTreeResult = await GetCallTreeTool.ExecuteAsync(
            server,
            new GetCallTreeInput(
                SymbolIdentifier: handle,
                Depth: 1,
                Format: "ascii",
                TopN: 5,
                Direction: "incoming"),
            CancellationToken.None);

        Assert.NotEqual(true, callTreeResult.IsError);
        var text = TextOf(callTreeResult);
        Assert.Contains("Greet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCallTree_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetCallTreeTool.ExecuteAsync(
            fixture.CreateServer(),
            new GetCallTreeInput(
                SymbolIdentifier: "h:unknown999",
                Depth: 1,
                Format: "ascii",
                TopN: 5,
                Direction: "outgoing"),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImpact_ConsumesOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        var findResult = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greet"],
            kind: "method",
            maxResults: 10,
            CancellationToken.None);
        var handle = Regex.Match(TextOf(findResult), @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`").Groups["handle"].Value;
        Assert.NotEmpty(handle);

        var impactResult = await GetImpactTool.ExecuteAsync(
            server,
            new GetImpactInput(
                GitRef: null,
                SymbolIdentifier: handle,
                MaxResults: 10,
                Depth: 1),
            CancellationToken.None);

        Assert.NotEqual(true, impactResult.IsError);
        var text = TextOf(impactResult);
        Assert.Contains("Greet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImpact_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetImpactTool.ExecuteAsync(
            fixture.CreateServer(),
            new GetImpactInput(
                GitRef: null,
                SymbolIdentifier: "h:unknown999",
                MaxResults: 10,
                Depth: 1),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependencyGraph_ConsumesOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        var findResult = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greeter"],
            kind: "class",
            maxResults: 10,
            CancellationToken.None);
        var handle = Regex.Match(TextOf(findResult), @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`").Groups["handle"].Value;
        Assert.NotEmpty(handle);

        var depResult = await DependencyGraphTool.ExecuteAsync(
            server,
            new DependencyGraphInput(
                FilePath: null,
                SymbolIdentifier: handle,
                Direction: "incoming",
                Depth: 1,
                MaxResults: 10),
            CancellationToken.None);

        Assert.NotEqual(true, depResult.IsError);
        var text = TextOf(depResult);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependencyGraph_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await DependencyGraphTool.ExecuteAsync(
            fixture.CreateServer(),
            new DependencyGraphInput(
                FilePath: null,
                SymbolIdentifier: "h:unknown999",
                Direction: "incoming",
                Depth: 1,
                MaxResults: 10),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFeatureContext_ConsumesOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        var findResult = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greet"],
            kind: "method",
            maxResults: 10,
            CancellationToken.None);
        var handle = Regex.Match(TextOf(findResult), @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`").Groups["handle"].Value;
        Assert.NotEmpty(handle);

        var featureResult = await GetFeatureContextTool.ExecuteAsync(
            server,
            new FeatureContextOptions(SymbolIdentifier: handle),
            CancellationToken.None);

        Assert.NotEqual(true, featureResult.IsError);
        var text = TextOf(featureResult);
        Assert.Contains("Greet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFeatureContext_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetFeatureContextTool.ExecuteAsync(
            fixture.CreateServer(),
            new FeatureContextOptions(SymbolIdentifier: "h:unknown999"),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTestContext_ConsumesOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();

        var findResult = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greeter"],
            kind: "class",
            maxResults: 10,
            CancellationToken.None);
        var handle = Regex.Match(TextOf(findResult), @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`").Groups["handle"].Value;
        Assert.NotEmpty(handle);

        var testResult = await GetTestContextTool.ExecuteAsync(
            server,
            new TestContextOptions(SymbolIdentifier: handle),
            CancellationToken.None);

        Assert.NotEqual(true, testResult.IsError);
        var text = TextOf(testResult);
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTestContext_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetTestContextTool.ExecuteAsync(
            fixture.CreateServer(),
            new TestContextOptions(SymbolIdentifier: "h:unknown999"),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }
}
