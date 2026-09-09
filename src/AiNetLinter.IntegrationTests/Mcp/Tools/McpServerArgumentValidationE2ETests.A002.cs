#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class McpServerHealthValidationA002E2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerHealthValidationA002E2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetServerHealth_NonPositiveDiagnosticLimit_ReturnsRecoverableInvalidArgument(int maxDiagnostics)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?>
            {
                ["includeDiagnostics"] = true,
                ["maxDiagnostics"] = maxDiagnostics,
            });

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.False(result.IsError, text);
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("maxDiagnostics", text, StringComparison.Ordinal);
        Assert.Equal(
            "INVALID_ARGUMENT",
            result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal(
            "$.maxDiagnostics",
            result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }
}
