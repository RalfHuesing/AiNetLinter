#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// D-003: Die beiden historischen Eingabeformen von <c>find_symbol</c> duerfen nicht
/// gleichzeitig angegeben und stillschweigend priorisiert werden.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpServerArgumentValidationD003E2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerArgumentValidationD003E2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSymbol_BothPatternFormsProvided_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["pattern"] = "Caller"
            });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("namePatterns", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("pattern", textContent.Text, StringComparison.Ordinal);
        Assert.Equal(
            "INVALID_ARGUMENT",
            result.StructuredContent!.Value.GetProperty("code").GetString());
    }
}
