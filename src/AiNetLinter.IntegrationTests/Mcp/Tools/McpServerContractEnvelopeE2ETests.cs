#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer Contract-v2 Navigation-Envelopes bei Fehlerzustaenden.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerContractEnvelopeE2ETests : IClassFixture<ReadOnlyMcpHostFixture>
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerContractEnvelopeE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TargetPathTool_MissingTargetPathReturnsFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolWithoutTargetAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "Greeter" } });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.True(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("targetPath", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.targetPath", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
        var navigation = result.StructuredContent.Value.GetProperty("navigation");
        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("INVALID_ARGUMENT", navigation.GetProperty("status").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetFeatureContext_MissingSymbolIdentifier_ReturnsErrorEnvelopeWithNavigationStatus()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_feature_context",
            new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
        var structured = result.StructuredContent!.Value;
        Assert.Equal("$.symbolIdentifier", structured.GetProperty("fieldPath").GetString());
        var navigation = structured.GetProperty("navigation");
        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.Equal("error", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("INVALID_ARGUMENT", navigation.GetProperty("status").GetProperty("code").GetString());
    }
}
