#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

public sealed partial class FindReferencesToolTests
{
    [Fact]
    public async Task ExecuteAsync_Depth1_MatchesCurrentBehavior()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Caller.cs", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithSymbolIdentifier_ResolvesReferences()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), new FindReferencesRequest("Greeter.Greet", MaxResults: 50, Depth: 1), CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("Caller.cs", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIsStructuredOnly_AndCallSiteDoesNotRepeatRootMetadata()
    {
        var result = await FindReferencesTool.ExecuteAsync(
            _fixture.CreateServer(), "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("handoff=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id: `s:", text, StringComparison.Ordinal);

        var entry = result.StructuredContent!.Value.GetProperty("callSites")[0];
        Assert.StartsWith("s:", entry.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("member", entry.GetProperty("handoffKind").GetString());
        Assert.False(entry.TryGetProperty("handoff", out _));
        Assert.False(entry.TryGetProperty("targetPath", out _));
        Assert.False(entry.TryGetProperty("snapshot", out _));
        Assert.False(entry.TryGetProperty("allowedFollowUpTools", out _));

        var handoff = result.StructuredContent.Value.GetProperty("handoff");
        Assert.Equal("symbolIdentifier", handoff.GetProperty("acceptedAs").GetString());
        Assert.Equal(
            ["get_symbol_body", "find_references", "get_call_tree", "get_impact", "get_test_context"],
            handoff.GetProperty("followUpsByKind").GetProperty("member").Deserialize<string[]>(McpJsonOptions.Default)
            ?? throw new InvalidOperationException("Die Member-Folgewerkzeuge müssen serialisierbar sein."));
    }

    [Fact]
    public async Task ExecuteAsync_StructuredCallSiteIdCanBeUsedForSymbolBodyFollowUp()
    {
        var state = _fixture.CreateServer();
        var references = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);
        var id = references.StructuredContent!.Value.GetProperty("callSites")[0]
            .GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Die Call-Site muss eine Handoff-ID besitzen.");

        var body = await AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync(
            state, [id], 80, CancellationToken.None);

        Assert.NotEqual(true, body.IsError);
        Assert.Contains("Greet", Assert.IsType<TextContentBlock>(Assert.Single(body.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxResults_TruncatesAndEmitsCountHeader()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 2, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }
}
