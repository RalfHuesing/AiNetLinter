#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer erfolgreiche Tool-Aufrufe und fachliche Ergebnisformen.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class McpServerToolBehaviorE2ETests
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
    public async Task FindSymbol_KindClass_ExcludesRecordSymbolsBeforeLimit()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeting" },
                ["kind"] = "class",
            });

        Assert.False(result.IsError == true, result.ToString());
        var matches = result.StructuredContent!.Value.GetProperty("results")[0].GetProperty("matches").EnumerateArray().ToList();
        Assert.NotEmpty(matches);
        Assert.All(matches, match => Assert.Equal("class", match.GetProperty("kind").GetString()));
        Assert.DoesNotContain(matches, match => match.GetProperty("name").GetString()!.Contains("GreetingRecord", StringComparison.Ordinal));
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
    public async Task ClassStructure_SourceNavigationOverhead_ReconcilesTruncationAndNextStep()
    {
        var baseline = await _fixture.Client.CallToolAsync(
            "get_class_structure",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter",
                ["maxMembers"] = 200,
            });

        Assert.False(baseline.IsError == true, baseline.ToString());
        var baselinePayload = baseline.StructuredContent!.Value;
        var baselineText = Assert.IsType<TextContentBlock>(Assert.Single(baseline.Content)).Text;
        var navigationMarker = baselineText.IndexOf("## Navigation", StringComparison.Ordinal);
        Assert.True(navigationMarker > 0, baselineText);
        var sourcePayload = JsonNode.Parse(baselinePayload.GetRawText())!.AsObject();
        sourcePayload.Remove("navigation");
        var sourceText = baselineText[..navigationMarker].TrimEnd();
        var sourceBytes = Encoding.UTF8.GetByteCount(sourceText)
            + Encoding.UTF8.GetByteCount(sourcePayload.ToJsonString());
        var unboundedBytes = Encoding.UTF8.GetByteCount(baselineText)
            + Encoding.UTF8.GetByteCount(baselinePayload.GetRawText());
        var budget = Math.Max(2_048, sourceBytes);
        Assert.True(sourceBytes <= budget, $"Source-Nutzlast muss vor Navigation ins Budget passen: {sourceBytes} > {budget}.");
        Assert.True(unboundedBytes > budget, $"Navigation muss den finalen Trim auslösen: {unboundedBytes} <= {budget}.");

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
        Assert.True(payload.TryGetProperty("navigation", out var navigation), payload.GetRawText());
        var next = navigation.GetProperty("next");
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.True(payload.GetProperty("truncated").GetBoolean(), payload.GetRawText());
        Assert.Contains("maxResponseBytes", payload.GetProperty("truncatedBy").ToString(), StringComparison.Ordinal);
        Assert.Equal("truncated", navigation.GetProperty("completeness").GetString());
        Assert.Equal("request_detail", next.GetProperty("kind").GetString());
        Assert.Contains("- completeness: `truncated`", text, StringComparison.Ordinal);
        Assert.Contains("- next: `request_detail`", text, StringComparison.Ordinal);
        Assert.True(
            Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(payload.GetRawText()) <= budget,
            $"Finale Source-Wire-Nutzlast überschreitet maxResponseBytes: {Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(payload.GetRawText())} > {budget}.");
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
    public async Task GetFileSkeleton_HandoffIdCanBePassedToGetSymbolBody()
    {
        var skeleton = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/SymbolGraphMini/Greeter.cs" } });

        Assert.NotNull(skeleton.StructuredContent);
        var type = Assert.Single(skeleton.StructuredContent!.Value.GetProperty("files")[0].GetProperty("types").EnumerateArray());
        var member = Assert.Single(type.GetProperty("members").EnumerateArray(), m =>
            m.GetProperty("signature").GetString()!.Contains("Greet", StringComparison.Ordinal));
        var id = member.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));

        var body = await _fixture.Client.CallToolAsync(
            "get_symbol_body",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = new[] { id } });

        Assert.False(body.IsError == true, body.ToString());
        Assert.Contains("Greet", Assert.IsType<TextContentBlock>(Assert.Single(body.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FeatureContext_CallerHandoffIdCanBePassedToFeatureContext()
    {
        var context = await _fixture.Client.CallToolAsync(
            "get_feature_context",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                ["maxCallers"] = 1,
            });

        Assert.NotNull(context.StructuredContent);
        Assert.True(context.StructuredContent!.Value.TryGetProperty("impact", out var impact), context.StructuredContent.Value.GetRawText());
        Assert.True(impact.TryGetProperty("callSites", out var callSites), context.StructuredContent.Value.GetRawText());
        var caller = Assert.Single(callSites.EnumerateArray());
        var callerId = caller.GetProperty("callerId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(callerId));
        Assert.True(caller.GetProperty("callerLocation").GetProperty("startLine").GetInt32() > 0);

        var followUp = await _fixture.Client.CallToolAsync(
            "get_feature_context",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = callerId,
            });

        Assert.False(followUp.IsError == true, followUp.ToString());
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
}
