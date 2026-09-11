#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

[Trait("Category", "Component")]
public sealed class GetCallTreeToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetCallTreeToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("irrelevant", 2, null, 10), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("DoesNotExistXyz", 2, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbol()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Run", 2, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AsciiFormatDefault_ReturnsTreeWithCallerNames()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.Run", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        // ASCII-Baum: Kindzeilen tragen den Renderer-eigenen Praefix.
        Assert.Contains("├──", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCallTree_ReturnsStructuredSuccessPayload()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<CallTreePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("incoming", payload!.Direction);
        Assert.Equal(1, payload.RequestedDepth);
        Assert.Equal(10, payload.TopN);
        Assert.False(payload.Truncated);
        Assert.False(payload.TopNTruncated);
        Assert.NotEmpty(payload.Graph.Nodes);
        Assert.Contains(payload.Graph.Nodes, child => child.Name.Contains("Caller", StringComparison.Ordinal));
        Assert.NotEmpty(payload.Graph.Edges);
    }

    [Fact]
    public async Task ExecuteAsync_UsesGraphDomainForBothTextFormatsWithoutHandoffIds()
    {
        var state = _fixture.CreateServer();

        var ascii = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, "ascii", 10), CancellationToken.None);
        var mermaid = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, "mermaid", 10), CancellationToken.None);

        var asciiText = Assert.IsType<TextContentBlock>(Assert.Single(ascii.Content)).Text;
        var mermaidText = Assert.IsType<TextContentBlock>(Assert.Single(mermaid.Content)).Text;
        Assert.Contains("Caller.Run", asciiText, StringComparison.Ordinal);
        Assert.Contains("Caller.Run", mermaidText, StringComparison.Ordinal);
        Assert.DoesNotContain("handoff=true", asciiText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("handoff=true", mermaidText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=", asciiText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=", mermaidText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytesTruncatesWholeEdgesWithMetadata()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 2, null, 10, MaxResponseBytes: 1_024), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<CallTreePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Contains("maxResponseBytes", payload!.TruncatedBy ?? []);
        Assert.True(payload.Truncated);
        Assert.Equal(payload.Graph.Nodes.Count, payload.Graph.Nodes.Select(node => node.NodeId).Distinct().Count());
    }

    [Fact]
    public async Task ApplyFinalResponseBudget_RebudgetsNavigationAndKeepsDeterministicReason()
    {
        var state = _fixture.CreateServer();
        var initial = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 2, null, 10, MaxResponseBytes: 32 * 1024), CancellationToken.None);
        var structuredNode = JsonNode.Parse(initial.StructuredContent!.Value.GetRawText())!.AsObject();
        structuredNode["navigation"] = new JsonObject
        {
            ["status"] = new JsonObject
            {
                ["operation"] = "ok",
                ["completeness"] = "truncated",
            },
        };
        var oversized = new CallToolResult
        {
            Content = [new TextContentBlock { Text = new string('x', 1_500) + "\nStatus: operation=ok, completeness=truncated" }],
            StructuredContent = JsonSerializer.SerializeToElement(structuredNode, McpJsonOptions.Default),
        };

        var result = CallGraphResponseBudget.ApplyFinalResponseBudget(oversized, 1_024);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var structured = result.StructuredContent!.Value;
        var combinedBytes = Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(structured.GetRawText());
        Assert.True(combinedBytes <= 1_024, $"Combined response was {combinedBytes} bytes.");
        Assert.True(structured.TryGetProperty("truncatedBy", out var truncatedBy), structured.GetRawText());
        Assert.Equal("maxResponseBytes", truncatedBy[0].GetString());
        Assert.Equal(
            "truncated",
            structured.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Fact]
    public void ApplyFinalResponseBudget_PreservesAssemblyEnvelopeAndDetectsMermaidAfterHeader()
    {
        var graph = new CallGraphPayload(
            "n1",
            [
                new CallGraphNode("n1", "M:Assembly.Root", "Root", "Root.cs:1", "method"),
                new CallGraphNode("n2", "M:Assembly.Caller", "Caller", "Caller.cs:2", "method"),
            ],
            [new CallGraphEdge("n2", "n1", [new CallGraphCallSite("Caller.cs", 2, 1, "Assembly")])]);
        var result = McpToolResults.Text(
            "[ASSEMBLY] targetPath=sample.dll; origin=decompiled\n\nflowchart TD\n    n2 --> n1\n" + new string('x', 3_000),
            new CallTreePayload(graph, "incoming", 2, 2, false, 10, false, false));
        var structured = JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!.AsObject();
        structured["analysis"] = new JsonObject
        {
            ["targetPath"] = "sample.dll",
            ["origin"] = "decompiled",
            ["confidence"] = "medium",
        };
        structured["wireBudget"] = new JsonObject
        {
            ["limitBytes"] = 32_768,
            ["textBytes"] = 3_000,
            ["structuredBytes"] = 500,
            ["totalBytes"] = 3_500,
        };
        result.StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default);

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 4_096);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text;
        var payload = limited.StructuredContent!.Value;
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("-->", text, StringComparison.Ordinal);
        Assert.Equal("decompiled", payload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.Equal("sample.dll", payload.GetProperty("analysis").GetProperty("targetPath").GetString());
        Assert.True(payload.TryGetProperty("wireBudget", out _), payload.GetRawText());
    }

    [Fact]
    public void FormatAssemblyCallGraphResponse_UsesSharedGraphForBothFormatsAndPreservesNavigation()
    {
        var graph = new CallGraphPayload(
            "n1",
            [
                new CallGraphNode("n1", "a:root", "Root", "Root.cs:1 [assembly=root.dll; origin=decompiled]", "method"),
                new CallGraphNode("n2", "a:caller", "Caller", "Caller.cs:2 [assembly=dependency.dll; origin=source-backed]", "method"),
            ],
            [new CallGraphEdge("n2", "n1", [new CallGraphCallSite("Caller.cs", 2, 1, "Dependency")])]);
        var navigation = new AssemblyNavigationSummary(
            true,
            2,
            2,
            false,
            "complete",
            []);

        var ascii = TransitiveCallGraphFormatter.FormatAssemblyCallGraphResponse(
            new AssemblyCallGraphResponseRequest(
                graph,
                CallTreeDirection.Incoming,
                "ascii",
                navigation,
                [],
                false,
                2,
                2,
                false,
                10,
                McpScopeType.All,
                false,
                32 * 1024));
        var mermaid = TransitiveCallGraphFormatter.FormatAssemblyCallGraphResponse(
            new AssemblyCallGraphResponseRequest(
                graph,
                CallTreeDirection.Incoming,
                "mermaid",
                navigation,
                [],
                false,
                2,
                2,
                false,
                10,
                McpScopeType.All,
                false,
                32 * 1024));

        var asciiPayload = JsonSerializer.Deserialize<CallTreePayload>(
            ascii.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        var mermaidPayload = JsonSerializer.Deserialize<CallTreePayload>(
            mermaid.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(asciiPayload);
        Assert.NotNull(mermaidPayload);
        Assert.Equal(asciiPayload!.Graph.Edges, mermaidPayload!.Graph.Edges);
        Assert.Equal(2, asciiPayload.AssemblyNavigation!.TotalAssemblyCount);
        Assert.True(ascii.StructuredContent.Value.GetProperty("navigation").GetProperty("includeReferences").GetBoolean());
        Assert.Contains("Caller", Assert.IsType<TextContentBlock>(Assert.Single(ascii.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("Caller", Assert.IsType<TextContentBlock>(Assert.Single(mermaid.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("assembly=dependency.dll", Assert.IsType<TextContentBlock>(Assert.Single(mermaid.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFinalResponseBudget_AssemblyGraphTrimsWholeEdgesAndKeepsMetadata()
    {
        var nodes = new List<CallGraphNode>
        {
            new("n1", "a:root", "Root", "Root.cs:1 [assembly=root.dll; origin=decompiled]", "method"),
        };
        var edges = new List<CallGraphEdge>();
        for (var index = 2; index <= 8; index++)
        {
            var nodeId = $"n{index}";
            nodes.Add(new CallGraphNode(
                nodeId,
                $"a:caller-{index}",
                $"Caller{index}",
                new string('x', 180),
                "method"));
            edges.Add(new CallGraphEdge(
                nodeId,
                "n1",
                [new CallGraphCallSite($"Caller{index}.cs", index, 1, "Dependency")],
                "virtual"));
        }

        var initial = TransitiveCallGraphFormatter.FormatAssemblyCallGraphResponse(
            new AssemblyCallGraphResponseRequest(
                new CallGraphPayload("n1", nodes, edges),
                CallTreeDirection.Incoming,
                "mermaid",
                new AssemblyNavigationSummary(true, 2, 2, false, "complete", []),
                [],
                false,
                1,
                1,
                false,
                10,
                McpScopeType.All,
                false,
                32 * 1024));
        var structured = JsonNode.Parse(initial.StructuredContent!.Value.GetRawText())!.AsObject();
        structured["analysis"] = new JsonObject
        {
            ["targetPath"] = "root.dll",
            ["origin"] = "decompiled",
            ["snapshot"] = "snapshot-1",
        };
        structured["wireBudget"] = new JsonObject
        {
            ["limitBytes"] = 32 * 1024,
            ["totalBytes"] = 8 * 1024,
        };
        var oversized = new CallToolResult
        {
            Content = [new TextContentBlock { Text = Assert.IsType<TextContentBlock>(Assert.Single(initial.Content)).Text }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default),
        };

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(oversized, 4_096);
        var payload = JsonSerializer.Deserialize<CallTreePayload>(
            limited.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.InRange(payload!.Graph.Edges.Count, 1, edges.Count - 1);
        Assert.All(payload.Graph.Edges, edge => Assert.Equal("virtual", edge.DispatchKind));
        Assert.All(payload.Graph.Edges, edge => Assert.Single(edge.CallSites));
        Assert.Equal("decompiled", limited.StructuredContent.Value.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.Equal("snapshot-1", limited.StructuredContent.Value.GetProperty("analysis").GetProperty("snapshot").GetString());
        Assert.True(limited.StructuredContent.Value.TryGetProperty("wireBudget", out _));
    }

    [Fact]
    public void ApplyFinalResponseBudget_PreservesAssemblyPrefixDiagnosticsDepthAndNavigation()
    {
        var nodes = new List<CallGraphNode>
        {
            new("n1", "a:root", "Root", "Root.cs:1 [assembly=root.dll; origin=decompiled]", "method"),
        };
        var edges = new List<CallGraphEdge>();
        for (var index = 2; index <= 14; index++)
        {
            var nodeId = $"n{index}";
            nodes.Add(new CallGraphNode(nodeId, $"a:caller-{index}", $"Caller{index}", new string('x', 180), "method"));
            edges.Add(new CallGraphEdge(
                nodeId,
                "n1",
                [new CallGraphCallSite($"Caller{index}.cs", index, 1, "Dependency")],
                "virtual"));
        }

        var graph = new CallGraphPayload("n1", nodes, edges);
        var graphText = GetCallTreeTool.RenderGraph(graph, "mermaid");
        const string prefix = "[ASSEMBLY] targetPath=root.dll; origin=decompiled; confidence=high\n\n";
        const string suffix = "\n\n[depth auf 5 begrenzt — requestedDepth=9]\n" +
            "[Assembly-Diagnostic] dependency.dll konnte nicht aufgelöst werden\n" +
            "[1 Diagnosen gesamt, 1 Samples gezeigt — gekürzt: keine]\n" +
            "Status: operation=get_call_tree, completeness=partial";
        var structured = JsonNode.Parse(JsonSerializer.SerializeToNode(
            new CallTreePayload(graph, "incoming", 9, 5, true, 10, false, false),
            McpJsonOptions.Default)!.ToJsonString())!.AsObject();
        structured["analysis"] = new JsonObject
        {
            ["targetPath"] = "root.dll",
            ["origin"] = "decompiled",
            ["snapshot"] = "snapshot-1",
        };
        structured["wireBudget"] = new JsonObject
        {
            ["limitBytes"] = 32 * 1024,
            ["totalBytes"] = 12 * 1024,
        };
        structured["navigation"] = new JsonObject
        {
            ["snapshot"] = "snapshot-1",
            ["status"] = "partial",
        };
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = prefix + graphText + suffix }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default),
        };

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 4_096);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text;

        Assert.StartsWith(prefix, text, StringComparison.Ordinal);
        Assert.Contains("[depth auf 5 begrenzt", text, StringComparison.Ordinal);
        Assert.Contains("[Assembly-Diagnostic] dependency.dll", text, StringComparison.Ordinal);
        Assert.EndsWith("Status: operation=get_call_tree, completeness=partial", text, StringComparison.Ordinal);
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.True(text.Contains("maxResponseBytes", StringComparison.Ordinal));
        var payload = limited.StructuredContent!.Value;
        Assert.Equal("decompiled", payload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.Equal("snapshot-1", payload.GetProperty("analysis").GetProperty("snapshot").GetString());
        var wireBudget = payload.GetProperty("wireBudget");
        var textBytes = Encoding.UTF8.GetByteCount(text);
        var structuredBytes = Encoding.UTF8.GetByteCount(payload.GetRawText());
        Assert.Equal(textBytes, wireBudget.GetProperty("textBytes").GetInt32());
        Assert.Equal(structuredBytes, wireBudget.GetProperty("structuredBytes").GetInt32());
        Assert.Equal(textBytes + structuredBytes, wireBudget.GetProperty("totalBytes").GetInt32());
        Assert.Equal(4_096, wireBudget.GetProperty("limitBytes").GetInt32());
        Assert.True(wireBudget.GetProperty("truncated").GetBoolean());
        Assert.True(payload.TryGetProperty("navigation", out _), payload.GetRawText());
    }

    [Fact]
    public void AssemblyCallGraphBuilder_GlobalHardCapLimitsMergedNodesAndEdgesDeterministically()
    {
        var nodes = Enumerable.Range(1, 260)
            .Select(index => new CallGraphNode(
                $"n{index}",
                $"a:symbol-{index}",
                $"Node{index}",
                $"Node{index}.cs:1",
                "method"))
            .ToList();
        var edges = Enumerable.Range(2, 259)
            .Select(index => new CallGraphEdge(
                $"n{index}",
                "n1",
                [new CallGraphCallSite($"Node{index}.cs", 1, 1, "Assembly")]))
            .Concat(Enumerable.Range(1, 50)
                .Select(index => new CallGraphEdge(
                    "n1",
                    "n1",
                    [new CallGraphCallSite("Root.cs", index, 1, "Assembly")])))
            .ToList();
        var graph = new CallGraphPayload("n1", nodes, edges);

        var first = AssemblyCallGraphBuilder.ApplyGlobalHardCap(graph);
        var second = AssemblyCallGraphBuilder.ApplyGlobalHardCap(graph);

        Assert.Equal(CallGraphTreeBuilder.MaxCallTreeNodes, first.Nodes.Count);
        Assert.Equal(CallGraphTreeBuilder.MaxCallTreeNodes, first.Edges.Count);
        Assert.True(first.HardCapTruncated);
        Assert.Equal("n1", first.RootNodeId);
        Assert.Equal(first.Nodes.Select(node => node.NodeId), second.Nodes.Select(node => node.NodeId));
        Assert.Equal(
            first.Edges.Select(edge => (edge.FromNodeId, edge.ToNodeId)),
            second.Edges.Select(edge => (edge.FromNodeId, edge.ToNodeId)));
    }

    [Fact]
    public void RenderTree_AssemblyProjectionDoesNotExposeHandoffIds()
    {
        var tree = new MetricsTreeNode(
            "Caller.Run",
            string.Empty,
            0,
            0,
            "Caller.cs:8",
            Array.Empty<MetricsTreeNode>(),
            Handoff: true,
            Id: "a:secret");

        var text = GetCallTreeTool.RenderTree(tree, null, 10, includeHandoffMetadata: false);

        Assert.DoesNotContain("handoff", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a:secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFinalResponseBudget_RejectsOversizedLegacyAssemblyEnvelope()
    {
        var navigation = new AssemblyNavigationSummary(
            false,
            1,
            1,
            false,
            "complete",
            Array.Empty<string>());
        var root = new MetricsTreeNode(
            "Root",
            string.Empty,
            0,
            0,
            "Root.cs:1",
            Enumerable.Range(0, 20)
                .Select(index => new MetricsTreeNode(
                    $"Child{index}",
                    string.Empty,
                    0,
                    0,
                    new string('x', 100),
                    Array.Empty<MetricsTreeNode>()))
                .ToList());
        var result = McpToolResults.Text(
            new string('x', 2_000),
            new AssemblyCallTreeResult(root, navigation, false));

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 1_024);

        Assert.Contains("maxResponseBytes", Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildGraphAsync_StopsAtMaxCallTreeNodesAndMarksNodeLimit()
    {
        var callers = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 260).Select(index => $"    public void Caller{index}() => Target();"));
        using var context = new McpInMemoryTestContext(
            McpInMemoryTestContext.CreateScenario(
                new ProjectSpec("Cap", [("Calls.cs", $$"""
                    namespace Cap;
                    public sealed class Calls
                    {
                        public void Target() { }
                    {{callers}}
                    }
                    """)])));
        using var state = context.CreateServer();
        var compilation = await context.Solution.Projects.Single().GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("Cap.Calls")!
            .GetMembers("Target").Single();

        var graph = await CallGraphTreeBuilder.BuildGraphAsync(
            new CallTreeBuildRequest(context.Solution, symbol, 1, 300, CallTreeDirection.Incoming),
            CancellationToken.None);

        Assert.InRange(graph.Nodes.Count, 1, CallGraphTreeBuilder.MaxCallTreeNodes);
        Assert.True(graph.HardCapTruncated);
    }

    [Fact]
    public async Task BuildGraphAsync_DoesNotExpandQueuedNodesAfterHardCap()
    {
        var callers = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 260).Select(index =>
                $"    public void Caller{index}() {{ Target(); {(index < 259 ? $"Caller{index + 1}();" : string.Empty)} }}"));
        using var context = new McpInMemoryTestContext(
            McpInMemoryTestContext.CreateScenario(
                new ProjectSpec("CapStop", [($"Calls.cs", $$"""
                    namespace CapStop;
                    public sealed class Calls
                    {
                        public void Target() { }
                    {{callers}}
                    }
                    """)])));
        using var state = context.CreateServer();
        var compilation = await context.Solution.Projects.Single().GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("CapStop.Calls")!
            .GetMembers("Target").Single();

        var graph = await CallGraphTreeBuilder.BuildGraphAsync(
            new CallTreeBuildRequest(context.Solution, symbol, 2, 300, CallTreeDirection.Incoming),
            CancellationToken.None);

        Assert.True(graph.HardCapTruncated);
        Assert.All(graph.Edges, edge => Assert.Equal(graph.RootNodeId, edge.ToNodeId));
    }

    [Fact]
    public async Task BuildTreeAsync_AssemblyReferenceScopeFiltersBeforeFanOut()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\CallTreeScope.slnx",
            new ProjectSpec("App", [
                ("Service.cs", "namespace App; public class Service { public void Run() {} }"),
                ("ProductionCaller.cs", "namespace App; public class ProductionCaller { public void Call(Service service) => service.Run(); }")]),
            new ProjectSpec("App.Tests", [
                ("ServiceTests.cs", "namespace App.Tests; public class ServiceTests { public void Call(App.Service service) => service.Run(); }")],
                ["App"]));
        var compilation = await solution.Solution.Projects.First(project => project.Name == "App")
            .GetCompilationAsync();
        var symbol = compilation!.GetTypeByMetadataName("App.Service")!
            .GetMembers("Run").Single();

        var (root, _) = await CallGraphTreeBuilder.BuildTreeAsync(
            new CallTreeBuildRequest(
                solution.Solution,
                symbol,
                1,
                10,
                CallTreeDirection.Incoming,
                ScopeType: McpScopeType.Production),
            CancellationToken.None);

        var child = Assert.Single(root.Children);
        Assert.Contains("ProductionCaller", child.Name, StringComparison.Ordinal);
        Assert.DoesNotContain(root.Children, node => node.Name.Contains("ServiceTests", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MermaidFormat_ReturnsFlowchartBlock()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, "mermaid", 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("flowchart TD", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("-->", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Caller.Run", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TopNBelowCallerCount_AppendsRemainingCountLine()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Greeter.Greet hat 3 distinkte Aufrufer (Run/RunTwice/RunThrice) — topN=1 zeigt 1,
        // der Renderer haengt die "... und N weitere"-Zeile an.
        Assert.Contains("... und 2 weitere", textContent.Text, StringComparison.Ordinal);
        // Regression: eine reine Renderer-Top-N-Kappung (kein 250-Knoten-Hardcap) muss trotzdem
        // als trunkiert erkannt werden — sonst behauptet der Sufficiency-Hinweis faelschlich
        // Vollstaendigkeit, obwohl sichtbar Kinder fehlen.
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("trunkiert", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("topN erhoehen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsAndStillReturnsResult()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 99, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("ValidClassA.DoWork", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OutgoingDirection_ReturnsCalleeNames()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("SymbolGraphMini.Caller.Run", 1, null, 10, "outgoing"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.Greet", text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[outgoing]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDirection_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10, "sideways"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("direction", text, StringComparison.Ordinal);
    }
}
