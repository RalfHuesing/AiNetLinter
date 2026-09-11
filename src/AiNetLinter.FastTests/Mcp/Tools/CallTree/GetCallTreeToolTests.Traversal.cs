#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.TestKit;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

// Traversal-Hardcap-Faelle liegen absichtlich neben den ausfuehrungsnahen Tool-Tests.
public sealed partial class GetCallTreeToolTests
{
    [Fact]
    public async Task BuildGraphAsync_StopsAtMaxCallTreeNodesAndMarksNodeLimit()
    {
        var callers = string.Join(Environment.NewLine, Enumerable.Range(0, 260)
            .Select(index => $"    public void Caller{index}() => Target();"));
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("Cap", [("Calls.cs", $$"""
                namespace Cap;
                public sealed class Calls
                {
                    public void Target() { }
                {{callers}}
                }
                """)])));
        var compilation = await context.Solution.Projects.Single().GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("Cap.Calls")!.GetMembers("Target").Single();
        var graph = await CallGraphTreeBuilder.BuildGraphAsync(
            new CallTreeBuildRequest(context.Solution, symbol, 1, 300, CallTreeDirection.Incoming), CancellationToken.None);
        Assert.InRange(graph.Nodes.Count, 1, CallGraphTreeBuilder.MaxCallTreeNodes);
        Assert.True(graph.HardCapTruncated);
    }

    [Fact]
    public async Task BuildGraphAsync_DoesNotExpandQueuedNodesAfterHardCap()
    {
        var callers = string.Join(Environment.NewLine, Enumerable.Range(0, 260).Select(index =>
            $"    public void Caller{index}() {{ Target(); {(index < 259 ? $"Caller{index + 1}();" : string.Empty)} }}"));
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("CapStop", [($"Calls.cs", $$"""
                namespace CapStop;
                public sealed class Calls
                {
                    public void Target() { }
                {{callers}}
                }
                """)])));
        var compilation = await context.Solution.Projects.Single().GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("CapStop.Calls")!.GetMembers("Target").Single();
        var graph = await CallGraphTreeBuilder.BuildGraphAsync(
            new CallTreeBuildRequest(context.Solution, symbol, 2, 300, CallTreeDirection.Incoming), CancellationToken.None);
        Assert.True(graph.HardCapTruncated);
        Assert.All(graph.Edges, edge => Assert.Equal(graph.RootNodeId, edge.ToNodeId));
    }

    [Fact]
    public async Task BuildTreeAsync_AssemblyReferenceScopeFiltersBeforeFanOut()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\CallTreeScope.slnx",
            new ProjectSpec("App", [("Service.cs", "namespace App; public class Service { public void Run() {} }"),
                ("ProductionCaller.cs", "namespace App; public class ProductionCaller { public void Call(Service service) => service.Run(); }")]),
            new ProjectSpec("App.Tests", [("ServiceTests.cs", "namespace App.Tests; public class ServiceTests { public void Call(App.Service service) => service.Run(); }")], ["App"]));
        var compilation = await solution.Solution.Projects.First(project => project.Name == "App").GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("App.Service")!.GetMembers("Run").Single();
        var (root, _) = await CallGraphTreeBuilder.BuildTreeAsync(new CallTreeBuildRequest(
            solution.Solution, symbol, 1, 10, CallTreeDirection.Incoming, ScopeType: McpScopeType.Production), CancellationToken.None);
        var child = Assert.Single(root.Children);
        Assert.Contains("ProductionCaller", child.Name, StringComparison.Ordinal);
        Assert.DoesNotContain(root.Children, node => node.Name.Contains("ServiceTests", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MermaidFormat_ReturnsFlowchartBlock()
    {
        var state = _fixture.CreateServer();
        var result = await GetCallTreeTool.ExecuteAsync(state, new GetCallTreeInput("Greeter.Greet", 1, "mermaid", 10), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("-->", text, StringComparison.Ordinal);
        Assert.Contains("Caller.Run", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TopNBelowCallerCount_AppendsRemainingCountLine()
    {
        var state = _fixture.CreateServer();
        var result = await GetCallTreeTool.ExecuteAsync(state, new GetCallTreeInput("Greeter.Greet", 1, null, 1), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("... und 2 weitere", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", text, StringComparison.Ordinal);
        Assert.Contains("trunkiert", text, StringComparison.Ordinal);
        Assert.Contains("topN erhoehen", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsAndStillReturnsResult()
    {
        var state = _fixture.CreateServer();
        var result = await GetCallTreeTool.ExecuteAsync(state, new GetCallTreeInput("Greeter.Greet", 99, null, 10), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
    }
}
