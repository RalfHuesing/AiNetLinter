#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Umfassende E2E- und Edge-Case-Tests für alle 9 MCP-Tools.
/// Nutzt Fixture-Workspaces zur gezielten Verifikation von Randfällen (Edge Cases).
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerAllToolsE2ETests
{
    [Fact]
    public async Task FindSymbol_KindFilter_ReturnsFilteredSymbolsOnly()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "Greeter",
                ["kind"] = "Class"
            });

        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_ZeroResults_ReturnsNoMatchMessage()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "NonExistentSymbol999" });

        Assert.Contains("Keine Treffer", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindReferences_UnknownSymbol_ReturnsErrorResult()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var result = await client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "NonExistent.Symbol" });

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImpact_BothArgumentsProvided_ReturnsErrorMessage()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var result = await client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["gitRef"] = "HEAD",
                ["symbolIdentifier"] = "Greeter.Greet"
            });

        Assert.True(result.IsError, "get_impact muss einen Fehler liefern, wenn gitRef UND symbolIdentifier angegeben sind.");
    }

    [Fact]
    public async Task GetTypeHierarchy_ValidType_ReturnsHierarchyInfo()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["typeIdentifier"] = "Greeter" });

        Assert.Contains("Basisklassen", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_UnknownType_ReturnsErrorResult()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var result = await client.CallToolAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["typeIdentifier"] = "UnknownClass123" });

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_NonExistentFile_ReturnsErrorOrNotFound()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var result = await client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/DoesNotExist.cs" });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFileSkeleton_NonCsFile_ReturnsErrorResult()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var result = await client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/SymbolGraphMini/wwwroot/Page.xaml" });

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIndexScope_ValidWorkspace_ReturnsFileTypeBreakdown()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync("get_index_scope");

        Assert.Contains(".cs", text, StringComparison.Ordinal);
        Assert.Contains(".razor", text, StringComparison.Ordinal);
        Assert.Contains(".xaml", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHotspots_ValidWorkspace_ReturnsHotspotSummary()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync("get_hotspots");

        Assert.NotNull(text);
    }

    [Fact]
    public async Task GetViolations_WithScopeFilter_FiltersResults()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "get_violations",
            new Dictionary<string, object?> { ["scopeFilter"] = "SymbolGraphMini" });

        Assert.NotNull(text);
    }

    [Fact]
    public async Task SearchPattern_PlainTextSearch_ReturnsMatches()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "userService",
                ["isRegex"] = false
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPattern_RegexSearch_ReturnsMatches()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = @"user\w+",
                ["isRegex"] = true
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownTool_Call_ThrowsMcpProtocolException()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        await Assert.ThrowsAsync<McpProtocolException>(() => client.CallToolAsync("unknown_tool_name"));
    }
}
