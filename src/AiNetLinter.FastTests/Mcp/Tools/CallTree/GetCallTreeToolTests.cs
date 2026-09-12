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
public sealed partial class GetCallTreeToolTests
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Caller", text, StringComparison.Ordinal);
        Assert.Contains("Greeter.Greet", text, StringComparison.Ordinal);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(Encoding.UTF8.GetByteCount(text) <= 1_024);
        Assert.Contains("Greeter.Greet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyFinalResponseBudget_RejectsOversizedContentWithActionableError()
    {
        var oversized = new CallToolResult
        {
            Content = [new TextContentBlock { Text = new string('x', 1_500) + "\nStatus: operation=ok, completeness=truncated" }],
        };

        var result = CallGraphResponseBudget.ApplyFinalResponseBudget(oversized, 1_024);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("maxResponseBytes", text, StringComparison.Ordinal);
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
            "[ASSEMBLY] targetPath=sample.dll; origin=decompiled\n\nflowchart TD\n    n2 --> n1\n" + new string('x', 3_000));

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 4_096);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text;
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("-->", text, StringComparison.Ordinal);
        Assert.Contains("origin=decompiled", text, StringComparison.Ordinal);
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
        var oversized = new CallToolResult
        {
            Content = [new TextContentBlock { Text = Assert.IsType<TextContentBlock>(Assert.Single(initial.Content)).Text }],
        };

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(oversized, 4_096);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text;
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("Caller2", text, StringComparison.Ordinal);
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
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = prefix + graphText + suffix }],
        };

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 4_096);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text;

        Assert.StartsWith(prefix, text, StringComparison.Ordinal);
        Assert.Contains("[depth auf 5 begrenzt", text, StringComparison.Ordinal);
        Assert.Contains("[Assembly-Diagnostic] dependency.dll", text, StringComparison.Ordinal);
        Assert.EndsWith("Status: operation=get_call_tree, completeness=partial", text, StringComparison.Ordinal);
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("origin=decompiled", text, StringComparison.Ordinal);
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
            new string('x', 2_000));

        var limited = CallGraphResponseBudget.ApplyFinalResponseBudget(result, 1_024);

        Assert.Contains("maxResponseBytes", Assert.IsType<TextContentBlock>(Assert.Single(limited.Content)).Text, StringComparison.Ordinal);
    }

}
