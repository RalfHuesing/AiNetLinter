#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Commands;

[Trait("Category", "Integration")]
public sealed class McpServerCommandMissHintTests : IClassFixture<SymbolGraphMcpFixture>
{
    private readonly SymbolGraphMcpFixture _fixture;

    public McpServerCommandMissHintTests(SymbolGraphMcpFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RunAsync_NonCsOnlyMatch_ReturnsExplicitMissHint()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "userService" });

        Assert.Contains("Keine Treffer fuer 'userService'", text, StringComparison.Ordinal);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", text, StringComparison.Ordinal);
        Assert.Contains("site.js", text, StringComparison.Ordinal);
        Assert.Contains("Component.razor", text, StringComparison.Ordinal);
        Assert.Contains("Page.xaml", text, StringComparison.Ordinal);
        Assert.Contains("search_pattern", text, StringComparison.Ordinal);
    }
}
