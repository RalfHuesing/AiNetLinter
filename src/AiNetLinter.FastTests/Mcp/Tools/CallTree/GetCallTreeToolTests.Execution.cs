#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

public sealed partial class GetCallTreeToolTests
{
    [Fact]
    public void AssemblyCallGraphBuilder_GlobalHardCapLimitsMergedNodesAndEdgesDeterministically()
    {
        var nodes = Enumerable.Range(1, 260).Select(index => new CallGraphNode(
            $"n{index}", $"a:symbol-{index}", $"Node{index}", $"Node{index}.cs:1", "method")).ToList();
        var edges = Enumerable.Range(2, 259).Select(index => new CallGraphEdge(
            $"n{index}", "n1", [new CallGraphCallSite($"Node{index}.cs", 1, 1, "Assembly")]))
            .Concat(Enumerable.Range(1, 50).Select(index => new CallGraphEdge(
                "n1", "n1", [new CallGraphCallSite("Root.cs", index, 1, "Assembly")]))).ToList();
        var graph = new CallGraphPayload("n1", nodes, edges);

        var first = AssemblyCallGraphBuilder.ApplyGlobalHardCap(graph);
        var second = AssemblyCallGraphBuilder.ApplyGlobalHardCap(graph);
        Assert.Equal(CallGraphTreeBuilder.MaxCallTreeNodes, first.Nodes.Count);
        Assert.Equal(CallGraphTreeBuilder.MaxCallTreeNodes, first.Edges.Count);
        Assert.True(first.HardCapTruncated);
        Assert.Equal("n1", first.RootNodeId);
        Assert.Equal(first.Nodes.Select(node => node.NodeId), second.Nodes.Select(node => node.NodeId));
        Assert.Equal(first.Edges.Select(edge => (edge.FromNodeId, edge.ToNodeId)), second.Edges.Select(edge => (edge.FromNodeId, edge.ToNodeId)));
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();
        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("ValidClassA.DoWork", 1, null, 10), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        Assert.DoesNotContain("Compile-Fehler", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OutgoingDirection_ReturnsCalleeNames()
    {
        var result = await GetCallTreeTool.ExecuteAsync(
            _fixture.CreateServer(), new GetCallTreeInput("SymbolGraphMini.Caller.Run", 1, null, 10, "outgoing"), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.Greet", text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[outgoing]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDirection_ReturnsRecoverableInvalidArgument()
    {
        var result = await GetCallTreeTool.ExecuteAsync(
            _fixture.CreateServer(), new GetCallTreeInput("Greeter.Greet", 1, null, 10, "sideways"), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("direction", text, StringComparison.Ordinal);
    }
}
