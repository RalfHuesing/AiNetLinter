#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    public async Task GetCallTree_ClampedDepthIsStructured()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                ["depth"] = 99,
            });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        Assert.Equal(99, payload.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(5, payload.GetProperty("effectiveDepth").GetInt32());
        Assert.True(payload.GetProperty("depthWasClamped").GetBoolean());
    }

    [Fact]
    public async Task FindReferences_ClampedDepthIsStructured()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                ["depth"] = 99,
            });

        Assert.False(result.IsError == true, result.ToString());
        var completeness = result.StructuredContent!.Value.GetProperty("completeness");
        Assert.Equal(99, completeness.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(3, completeness.GetProperty("effectiveDepth").GetInt32());
        Assert.True(completeness.GetProperty("depthWasClamped").GetBoolean());
    }

    [Fact]
    public async Task NamespaceTree_ClampedDepthIsStructured()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?> { ["depth"] = 99 });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        Assert.Equal(99, payload.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(3, payload.GetProperty("effectiveDepth").GetInt32());
        Assert.True(payload.GetProperty("depthWasClamped").GetBoolean());
    }

    [Fact]
    public async Task NamespaceTree_ResponseBudgetKeepsTextAndStructuredProjectionAligned()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["project"] = "SymbolGraphMini",
                ["namespacePrefix"] = "SymbolGraphMini",
                ["maxResponseBytes"] = 2048,
            });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        Assert.Contains("maxResponseBytes", payload.GetProperty("truncatedBy").ToString(), StringComparison.Ordinal);
        Assert.Equal("source", payload.GetProperty("navigation").GetProperty("origin").GetString());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(
            Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(payload.GetRawText()) <= 2048,
            "Finale kombinierte Wire-Nutzlast darf maxResponseBytes inklusive Navigation nicht überschreiten.");
        Assert.Contains("maxResponseBytes", text, StringComparison.Ordinal);
        foreach (var type in payload.GetProperty("types").EnumerateArray())
        {
            Assert.Contains(type.GetProperty("name").GetString() ?? string.Empty, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ClassStructure_ResponseBudgetIncludesNavigationAndStructuredContent()
    {
        const int budget = 4096;
        var result = await _fixture.Client.CallToolAsync(
            "get_class_structure",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter",
                ["maxMembers"] = 200,
                ["maxResponseBytes"] = budget,
            });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var totalBytes = Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(payload.GetRawText());
        Assert.True(totalBytes <= budget, $"Finale kombinierte Wire-Nutzlast überschreitet maxResponseBytes: {totalBytes} > {budget}.");
        Assert.Contains("## Navigation", text, StringComparison.Ordinal);
        Assert.Equal("source", payload.GetProperty("navigation").GetProperty("origin").GetString());
    }

    [Fact]
    public async Task DependencyGraph_ClampedDepthIsStructured()
    {
        var result = await _fixture.Client.CallToolAsync(
            "dependency_graph",
            new Dictionary<string, object?>
            {
                ["filePath"] = "src/SymbolGraphMini/Greeter.cs",
                ["depth"] = 99,
                ["maxResults"] = 10,
            });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        Assert.Equal(99, payload.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(3, payload.GetProperty("effectiveDepth").GetInt32());
        Assert.True(payload.GetProperty("depthWasClamped").GetBoolean());
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
    public async Task GetFileSkeleton_ResponseBudgetPreservesNavigationEnvelope()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?>
            {
                ["filePaths"] = new[] { "src/SymbolGraphMini/Greeter.cs" },
                ["maxResponseBytes"] = 2048,
            });

        Assert.False(result.IsError == true, result.ToString());
        Assert.NotNull(result.StructuredContent);
        Assert.Equal("source", result.StructuredContent!.Value.GetProperty("navigation").GetProperty("origin").GetString());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(result.StructuredContent.Value.GetRawText()) <= 2048);
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
    public async Task GetHotspots_WireParameters_ReturnBoundedStructuredPayload()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_hotspots",
            new Dictionary<string, object?>
            {
                ["maxResults"] = 1,
                ["minLinePercentage"] = 0,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.Equal(1, payload.GetProperty("maxResults").GetInt32());
        Assert.Equal(0, payload.GetProperty("minLinePercentage").GetDouble());
        Assert.True(payload.GetProperty("shownHotspots").GetInt32() <= 1);
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
        var navigation = structured.GetProperty("navigation");
        Assert.Equal("source", navigation.GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("operationStatus").GetString());
        Assert.Equal("supported", navigation.GetProperty("capabilities").GetProperty("navigation").GetString());
        Assert.Equal("complete", navigation.GetProperty("completeness").GetString());
        Assert.Equal("source-files", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.NotEqual(
            navigation.GetProperty("target").GetProperty("fingerprint").GetString(),
            navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString());
        Assert.Contains(
            "userService",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetServerHealth_TargetedResponse_UsesCommonNavigationProjection()
    {
        await _fixture.Client.CallToolGetTextAsync("get_hotspots");
        var targetPath = await _fixture.Client.GetTargetPathAsync();
        var result = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?> { ["targetPath"] = Path.GetFullPath(targetPath) });

        Assert.NotEqual(true, result.IsError);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("source", navigation.GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("operationStatus").GetString());
        Assert.Equal("supported", navigation.GetProperty("capabilities").GetProperty("navigation").GetString());
        Assert.False(string.IsNullOrWhiteSpace(navigation.GetProperty("target").GetProperty("targetPath").GetString()));
        Assert.Equal("source-files", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.True(navigation.GetProperty("snapshot").GetProperty("fresh").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString()));

        var followUp = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["targetPath"] = Path.GetFullPath(targetPath),
                ["namePatterns"] = new[] { "Greeter" },
            });
        Assert.False(followUp.IsError == true, followUp.ToString());
        Assert.Equal(
            navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString(),
            followUp.StructuredContent!.Value.GetProperty("navigation").GetProperty("snapshot").GetProperty("fingerprint").GetString());
    }
}
