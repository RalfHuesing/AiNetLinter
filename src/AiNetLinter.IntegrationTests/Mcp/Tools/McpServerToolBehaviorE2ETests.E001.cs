#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E-001: Ein leerer search_pattern-Treffer darf im Fachpayload und in der Navigation
/// nicht zwei widersprüchliche Folgeaktionen anbieten.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpServerToolBehaviorE001E2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerToolBehaviorE001E2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchPattern_EmptyResult_UsesSameNextActionInPayloadAndNavigation()
    {
        var result = await _fixture.Client.CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "E001-Pattern-That-Does-Not-Exist",
                ["isRegex"] = false,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);

        var payload = result.StructuredContent!.Value;
        var rootNext = payload.GetProperty("next");
        var navigationNext = payload.GetProperty("navigation").GetProperty("next");

        Assert.Equal("refine_scope", rootNext.GetProperty("kind").GetString());
        Assert.Equal(
            rootNext.GetProperty("kind").GetString(),
            navigationNext.GetProperty("kind").GetString());
        Assert.Equal(
            rootNext.GetProperty("action").GetString(),
            navigationNext.GetProperty("action").GetString());
    }
}
