#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer erfolgreiche Tool-Aufrufe und fachliche Ergebnisformen.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerToolBehaviorE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerToolBehaviorE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSymbol_KindFilter_ReturnsFilteredSymbolsOnly()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["kind"] = "Class"
            });

        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_ZeroResults_ReturnsNoMatchMessage()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "NonExistentSymbol999" } });

        Assert.Contains("Keine Treffer", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindReferences_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "NonExistent.Symbol" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_ValidType_ReturnsHierarchyInfo()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter" });

        Assert.Contains("Basisklassen", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_UnknownType_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "UnknownClass123" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_NonExistentFile_ReturnsErrorOrNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/DoesNotExist.cs" } });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFileSkeleton_NonCsFile_ReturnsRecoverableResourceNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/SymbolGraphMini/wwwroot/Page.xaml" } });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIndexScope_ValidWorkspace_ReturnsFileTypeBreakdown()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_index_scope");

        Assert.Contains(".cs", text, StringComparison.Ordinal);
        Assert.Contains(".razor", text, StringComparison.Ordinal);
        Assert.Contains(".xaml", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHotspots_ValidWorkspace_ReturnsHotspotSummary()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_hotspots");

        Assert.NotNull(text);
    }

    [Fact]
    public async Task GetViolations_WithScopeFilter_FiltersResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_violations",
            new Dictionary<string, object?> { ["scopeFilter"] = "SymbolGraphMini" });

        Assert.NotNull(text);
    }

    [Fact]
    public async Task SearchPattern_PlainTextSearch_ReturnsMatches()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
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
        var text = await _fixture.Client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = @"user\w+",
                ["isRegex"] = true
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPattern_StructuredResponse_ReturnsObjectWithRangesAndCompleteness()
    {
        var result = await _fixture.Client.CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "userService",
                ["contextLines"] = 1,
                ["maxResponseBytes"] = 4096,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = result.StructuredContent!.Value;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, structured.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, structured.GetProperty("matches").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, structured.GetProperty("completeness").ValueKind);
        Assert.Contains(
            "userService",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.OrdinalIgnoreCase);
    }
}

