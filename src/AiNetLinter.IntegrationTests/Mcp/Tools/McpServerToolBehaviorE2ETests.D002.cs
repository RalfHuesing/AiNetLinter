#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// D-002: Ein fehlender Dateibaum-Root bleibt eine verarbeitete, navigierbare Antwort.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpServerToolBehaviorD002E2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerToolBehaviorD002E2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetFileTree_NonExistentRoot_ReturnsResourceNotFoundWithErrorNavigation()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_tree",
            new Dictionary<string, object?>
            {
                ["root"] = "D002-Root-That-Does-Not-Exist"
            });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESOURCE_NOT_FOUND", text, StringComparison.Ordinal);

        Assert.NotNull(result.StructuredContent);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("resource_not_found", navigation.GetProperty("status").GetProperty("operation").GetString());
        Assert.Equal("not_applicable", navigation.GetProperty("status").GetProperty("completeness").GetString());
    }
}
