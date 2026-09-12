#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Repräsentative Wire- und Handoff-Verträge für die MCP-Tool-Gruppen.
/// Varianten, Limits und Ergebnisprojektionen liegen in den FastTests.
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
    public async Task FindSymbol_StandardCall_ReturnsStructuredResult()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "Greeter" } });

        Assert.False(result.IsError == true, result.ToString());
        Assert.Contains("Greeter", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
        Assert.NotEmpty(result.StructuredContent!.Value.GetProperty("results").EnumerateArray());
    }

    [Fact]
    public async Task GetNamespaceTree_StandardCall_ReturnsStructuredResult()
    {
        var result = await _fixture.Client.CallToolAsync("get_namespace_tree");

        Assert.False(result.IsError == true, result.ToString());
        Assert.Contains("SymbolGraphMini", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
        Assert.True(result.StructuredContent!.Value.TryGetProperty("projects", out _));
    }

    [Fact]
    public async Task GetFileSkeleton_HandoffIdCanBePassedToGetSymbolBody()
    {
        var skeleton = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/SymbolGraphMini/Greeter.cs" } });

        Assert.NotNull(skeleton.StructuredContent);
        var type = Assert.Single(skeleton.StructuredContent!.Value.GetProperty("files")[0].GetProperty("types").EnumerateArray());
        var member = Assert.Single(type.GetProperty("members").EnumerateArray(), item =>
            item.GetProperty("signature").GetString()!.Contains("Greet", StringComparison.Ordinal));
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
        var impact = context.StructuredContent!.Value.GetProperty("impact");
        var caller = Assert.Single(impact.GetProperty("callSites").EnumerateArray());
        var callerId = caller.GetProperty("callerId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(callerId));
        Assert.True(caller.GetProperty("callerLocation").GetProperty("startLine").GetInt32() > 0);

        var followUp = await _fixture.Client.CallToolAsync(
            "get_feature_context",
            new Dictionary<string, object?> { ["symbolIdentifier"] = callerId });

        Assert.False(followUp.IsError == true, followUp.ToString());
    }
}
