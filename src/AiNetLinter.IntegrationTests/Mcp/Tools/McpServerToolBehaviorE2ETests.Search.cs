#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
                ["isRegex"] = false,
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }
}
