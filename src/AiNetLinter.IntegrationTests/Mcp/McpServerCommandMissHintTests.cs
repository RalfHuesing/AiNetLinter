#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpServerCommandMissHintTests
{
    private readonly ReadOnlyMcpHostFixture fixture;

    public McpServerCommandMissHintTests(ReadOnlyMcpHostFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_NonCsOnlyMatch_ReturnsExplicitMissHint()
    {
        var host = await fixture.GetHostAsync();
        var text = await host.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "userService" } });

        Assert.Contains("Keine Treffer fuer 'userService'", text, StringComparison.Ordinal);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", text, StringComparison.Ordinal);
        Assert.Contains("site.js", text, StringComparison.Ordinal);
        Assert.Contains("Component.razor", text, StringComparison.Ordinal);
        Assert.Contains("Page.xaml", text, StringComparison.Ordinal);
        Assert.Contains("search_pattern", text, StringComparison.Ordinal);
    }
}
