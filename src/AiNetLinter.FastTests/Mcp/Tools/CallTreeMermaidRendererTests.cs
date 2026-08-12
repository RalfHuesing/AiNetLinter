#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Unit")]
public sealed class CallTreeMermaidRendererTests
{
    private static MetricsTreeNode Leaf(string name, string displayLine) =>
        new(name, "", 0, 0, displayLine, Array.Empty<MetricsTreeNode>());

    [Fact]
    public void Render_SingleNode_StartsWithFlowchartHeaderAndDeclaresRoot()
    {
        var root = Leaf("Greeter.Greet", "Greeter.cs:5");

        var text = CallTreeMermaidRenderer.Render(root, topN: 10);

        Assert.StartsWith("flowchart TD", text, StringComparison.Ordinal);
        Assert.Contains("n0[\"Greeter.Greet — Greeter.cs:5\"]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ParentChild_ProducesEdgeBetweenUniqueIds()
    {
        var child = Leaf("Caller.Run", "Caller.cs:8");
        var root = new MetricsTreeNode("Greeter.Greet", "", 0, 0, "Greeter.cs:5", new List<MetricsTreeNode> { child });

        var text = CallTreeMermaidRenderer.Render(root, topN: 10);

        Assert.Contains("n0[\"Greeter.Greet — Greeter.cs:5\"]", text, StringComparison.Ordinal);
        Assert.Contains("n1[\"Caller.Run — Caller.cs:8\"]", text, StringComparison.Ordinal);
        Assert.Contains("n0 --> n1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_TopNBelowChildCount_AppendsOverflowNode()
    {
        var children = new List<MetricsTreeNode>
        {
            Leaf("Caller.Run", "Caller.cs:8"),
            Leaf("Caller.RunTwice", "Caller.cs:14"),
            Leaf("Caller.RunThrice", "Caller.cs:20"),
        };
        var root = new MetricsTreeNode("Greeter.Greet", "", 0, 0, "Greeter.cs:5", children);

        var text = CallTreeMermaidRenderer.Render(root, topN: 2);

        Assert.Contains("Caller.Run", text, StringComparison.Ordinal);
        Assert.Contains("Caller.RunTwice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Caller.RunThrice", text, StringComparison.Ordinal);
        Assert.Contains("... und 1 weitere", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LabelWithQuotesAndNewline_EscapesForMermaidSyntax()
    {
        var root = Leaf("Weird\"Name", "path.cs:1\nmore");

        var text = CallTreeMermaidRenderer.Render(root, topN: 10);

        Assert.DoesNotContain("\"Weird\"Name", text, StringComparison.Ordinal);
        Assert.Contains("Weird'Name", text, StringComparison.Ordinal);
        Assert.Contains("path.cs:1 more", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NestedTopN_AppliesLimitAtEveryLevel()
    {
        var branch = new MetricsTreeNode("Branch.Run", "", 0, 0, "Branch.cs:4", new List<MetricsTreeNode>
        {
            Leaf("Child.First", "First.cs:8"),
            Leaf("Child.Second", "Second.cs:12"),
            Leaf("Child.Hidden", "Hidden.cs:16"),
        });
        var root = new MetricsTreeNode("Root.Run", "", 0, 0, "Root.cs:1", new List<MetricsTreeNode> { branch });

        var text = CallTreeMermaidRenderer.Render(root, topN: 2);

        Assert.Contains("Child.First", text, StringComparison.Ordinal);
        Assert.Contains("Child.Second", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Child.Hidden", text, StringComparison.Ordinal);
        Assert.Contains("n4[\"... und 1 weitere\"]", text, StringComparison.Ordinal);
        Assert.Contains("n1 --> n4", text, StringComparison.Ordinal);
    }
}
