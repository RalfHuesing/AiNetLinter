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
/// Repräsentative Wire-Verträge für die Fehlerhülle der MCP-Argumentvalidierung.
/// Varianten und Grenzwerte werden in den FastTests des verantwortlichen Validators geprüft.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerArgumentValidationE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerArgumentValidationE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UnknownTool_Call_ThrowsMcpProtocolException()
    {
        await Assert.ThrowsAsync<McpProtocolException>(() => _fixture.Client.CallToolAsync("unknown_tool_name"));
    }

    [Fact]
    public async Task FindReferences_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgumentWithNavigation()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_references", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.True(result.StructuredContent.Value.TryGetProperty("navigation", out _));
    }

    [Fact]
    public async Task WrongArrayElementType_ReturnsIndexedFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new object?[] { "Greeter", 42 },
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.True(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("namePatterns[1]", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.namePatterns[1]", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
        Assert.True(result.StructuredContent.Value.TryGetProperty("navigation", out _));
    }
}
