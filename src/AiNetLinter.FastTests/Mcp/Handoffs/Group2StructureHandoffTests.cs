#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class Group2StructureHandoffTests
{
    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    [Fact]
    public async Task GetFileSkeleton_EmitsOpaqueHandoffHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetFileSkeletonTool.ExecuteAsync(
            fixture.CreateServer(),
            ["Greeter.cs"],
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("handoffId: `h:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetClassStructure_EmitsOpaqueHandoffHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetClassStructureTool.ExecuteAsync(
            fixture.CreateServer(),
            "Greeter",
            "lines",
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("handoffId: `h:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetClassStructure_AcceptsOpaqueHandle_FromFindSymbol()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();
        var find = await FindSymbolTool.ExecuteAsync(
            server,
            namePatterns: ["Greeter"],
            kind: "class",
            maxResults: 10,
            CancellationToken.None);

        var findText = TextOf(find);
        var match = Regex.Match(findText, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`");
        Assert.True(match.Success, $"Expected h:... handle in: {findText}");
        var handle = match.Groups["handle"].Value;

        var structure = await GetClassStructureTool.ExecuteAsync(
            server,
            handle,
            "lines",
            CancellationToken.None);

        Assert.NotEqual(true, structure.IsError);
        var structureText = TextOf(structure);
        Assert.Contains("# Typ: SymbolGraphMini.Greeter", structureText, StringComparison.Ordinal);
        Assert.Contains("Greet", structureText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_EmitsOpaqueHandoffHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetTypeHierarchyTool.ExecuteAsync(
            fixture.CreateServer(),
            "BaseGreeting",
            GetTypeHierarchyTool.DefaultMaxResults,
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("handoffId: `h:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindImplementations_EmitsOpaqueHandoffHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindImplementationsTool.ExecuteAsync(
            fixture.CreateServer(),
            "IGreeting",
            FindImplementationsTool.DefaultMaxResults,
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("handoffId: `h:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("handoffId: `i:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_To_FindImplementations_Roundtrip_WithOpaqueHandle()
    {
        using var fixture = new McpInMemoryTestContext();
        var server = fixture.CreateServer();
        var hierarchy = await GetTypeHierarchyTool.ExecuteAsync(
            server,
            "BaseGreeting",
            GetTypeHierarchyTool.DefaultMaxResults,
            CancellationToken.None);

        var text = TextOf(hierarchy);
        var match = Regex.Match(text, @"handoffId:\s*`(?<handle>h:[a-zA-Z0-9]+)`");
        Assert.True(match.Success, $"Expected h:... handle in: {text}");
        var handle = match.Groups["handle"].Value;

        var implementations = await FindImplementationsTool.ExecuteAsync(
            server,
            handle,
            FindImplementationsTool.DefaultMaxResults,
            CancellationToken.None);

        Assert.NotEqual(true, implementations.IsError);
        var implText = TextOf(implementations);
        Assert.Contains("BaseGreeting", implText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetClassStructure_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await GetClassStructureTool.ExecuteAsync(
            fixture.CreateServer(),
            "h:unknown999",
            "lines",
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindImplementations_WithUnknownHandle_ReturnsHandoffUnknown()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindImplementationsTool.ExecuteAsync(
            fixture.CreateServer(),
            "h:unknown999",
            FindImplementationsTool.DefaultMaxResults,
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains(LinterErrorCodes.HandoffUnknown, text, StringComparison.Ordinal);
    }
}
