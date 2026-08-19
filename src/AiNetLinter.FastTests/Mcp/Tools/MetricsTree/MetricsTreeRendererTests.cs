#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.MetricsTree;

[Trait("Category", "Unit")]
public sealed class MetricsTreeRendererTests
{
    private static MetricsTreeNode Leaf(string name, double sortValue) =>
        new(name, name, 1, sortValue, $"1 Datei | {sortValue} LoC", Array.Empty<MetricsTreeNode>());

    [Fact]
    public void Render_SortsChildrenDescending_WhenRequested()
    {
        var root = new MetricsTreeNode("root", "", 3, 0, "3 Dateien", new List<MetricsTreeNode>
        {
            Leaf("small.cs", 10),
            Leaf("big.cs", 100),
            Leaf("medium.cs", 50),
        });

        var text = MetricsTreeRenderer.Render(root, topN: 10, sortDescending: true);
        var lines = text.Split('\n');

        Assert.Contains("big.cs", lines[1]);
        Assert.Contains("medium.cs", lines[2]);
        Assert.Contains("small.cs", lines[3]);
    }

    [Fact]
    public void Render_SortsChildrenAscending_WhenRequested()
    {
        var root = new MetricsTreeNode("root", "", 3, 0, "3 Dateien", new List<MetricsTreeNode>
        {
            Leaf("small.cs", 10),
            Leaf("big.cs", 100),
            Leaf("medium.cs", 50),
        });

        var text = MetricsTreeRenderer.Render(root, topN: 10, sortDescending: false);
        var lines = text.Split('\n');

        Assert.Contains("small.cs", lines[1]);
        Assert.Contains("medium.cs", lines[2]);
        Assert.Contains("big.cs", lines[3]);
    }

    [Fact]
    public void Render_TopNLimitsVisibleChildren_AndAppendsRemainingCount()
    {
        var root = new MetricsTreeNode("root", "", 5, 0, "5 Dateien", new List<MetricsTreeNode>
        {
            Leaf("a.cs", 5),
            Leaf("b.cs", 4),
            Leaf("c.cs", 3),
            Leaf("d.cs", 2),
            Leaf("e.cs", 1),
        });

        var text = MetricsTreeRenderer.Render(root, topN: 2, sortDescending: true);

        Assert.Contains("a.cs", text);
        Assert.Contains("b.cs", text);
        Assert.DoesNotContain("c.cs", text);
        Assert.Contains("... und 3 weitere", text);
    }

    [Fact]
    public void Render_NestedChildren_ProducesCorrectIndentation()
    {
        var child = new MetricsTreeNode("child.cs", "dir/child.cs", 1, 1, "1 Datei", Array.Empty<MetricsTreeNode>());
        var dir = new MetricsTreeNode("dir", "dir", 1, 1, "1 Datei", new List<MetricsTreeNode> { child });
        var root = new MetricsTreeNode("root", "", 1, 0, "1 Datei", new List<MetricsTreeNode> { dir });

        var text = MetricsTreeRenderer.Render(root, topN: 10, sortDescending: true);
        var lines = text.Split('\n');

        Assert.StartsWith("└── dir", lines[1]);
        Assert.StartsWith("    └── child.cs", lines[2]);
    }

    [Fact]
    public void Render_NestedTopN_AppliesLimitAtEveryLevel()
    {
        var dir = new MetricsTreeNode("dir", "dir", 3, 0, "3 Dateien", new List<MetricsTreeNode>
        {
            Leaf("small.cs", 10),
            Leaf("large.cs", 30),
            Leaf("medium.cs", 20),
        });
        var root = new MetricsTreeNode("root", "", 3, 0, "3 Dateien", new List<MetricsTreeNode> { dir });

        var text = MetricsTreeRenderer.Render(root, topN: 2, sortDescending: true);
        var lines = text.Split('\n');

        Assert.StartsWith("    ├── large.cs", lines[2]);
        Assert.StartsWith("    ├── medium.cs", lines[3]);
        Assert.StartsWith("    └── ... und 1 weitere", lines[4]);
        Assert.DoesNotContain("small.cs", text);
    }
}
