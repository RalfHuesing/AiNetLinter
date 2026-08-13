#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class MetricsTreeToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public MetricsTreeToolTests() { _fixture = new McpInMemoryTestContext(); }

    private McpCodeGraphServer NewState() =>
        _fixture.CreateServer();

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", 1, 10, null), CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownMode_ReturnsInvalidArgument()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "unknown_mode", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ExecuteAsync_DepthOutOfRange_ReturnsInvalidArgument(int depth)
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", depth, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text);
        Assert.Contains("depth", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TopNZeroOrNegative_ReturnsInvalidArgument()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", 1, 0, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text);
        Assert.Contains("top_n", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidFileFilterRegex_ReturnsInvalidArgument()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", 1, 10, "["), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text);
        Assert.Contains("file_filter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CodeSizeMode_ReturnsTreeSortedByLocDescending()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini", "code_size", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        // Hierarchy.cs (24 Zeilen) ist deutlich groesser als OtherCaller.cs (7 Zeilen) —
        // bei absteigender Sortierung muss Hierarchy.cs vor OtherCaller.cs erscheinen.
        Assert.True(text.IndexOf("Hierarchy.cs", StringComparison.Ordinal) <
                     text.IndexOf("OtherCaller.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_CommentDensityMode_ReturnsTreeSortedByRatioAscending()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini", "comment_density", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        // ViolationTrigger.cs ist die einzige Datei mit einer Kommentarzeile (Ratio > 0), alle
        // anderen haben Ratio 0 — bei aufsteigender Sortierung (niedrigste Ratio zuerst) muss
        // ViolationTrigger.cs nach Greeter.cs erscheinen.
        Assert.True(text.IndexOf("Greeter.cs", StringComparison.Ordinal) <
                     text.IndexOf("ViolationTrigger.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_RootNotMatchingAnyFile_ReturnsExplicitEmptyMessage()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("DoesNotExistAnywhere", "code_size", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Keine Dateien unter root=", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RootPointingToSingleFile_ReturnsSingleNodeTree()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini/Greeter.cs", "code_size", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text);
        Assert.DoesNotContain("├──", text, StringComparison.Ordinal);
        Assert.DoesNotContain("└──", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxDepth_DoesNotThrowAndClampsGracefully()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", 5, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text);
    }

    [Fact]
    public async Task ExecuteAsync_FileFilterExcludesMatchingFiles_NarrowsTree()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini", "code_size", 1, 10, "Greeter"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text);
        Assert.DoesNotContain("Caller.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hierarchy.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ViolationTrigger.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ContainsDrillDownHint()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "code_size", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("[HINWEIS]", text, StringComparison.Ordinal);
        Assert.Contains("Top-N-Ausschnitt", text, StringComparison.Ordinal);
    }
}
