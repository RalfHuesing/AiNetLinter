#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

public sealed partial class McpServerToolBehaviorE2ETests
{
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
        Assert.Equal("source", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("complete", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("source", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            navigation.GetProperty("snapshot").GetProperty("fingerprint").GetString()));
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
        Assert.Equal("source", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.False(string.IsNullOrWhiteSpace(navigation.GetProperty("target").GetProperty("targetPath").GetString()));
        Assert.Equal("source", navigation.GetProperty("snapshot").GetProperty("kind").GetString());
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
