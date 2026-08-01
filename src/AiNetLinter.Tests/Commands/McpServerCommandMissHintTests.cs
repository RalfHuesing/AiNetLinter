#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Test fuer EPIC-07 Miss-Hint-Vollstaendigkeit (Konzept Z. 612-615): eine Anfrage
/// nach einem Namen, der nur in .js/.razor/.xaml vorkommt (<c>userService</c>), liefert
/// die explizite Miss-Hint-Meldung statt einer stillen Leermenge. Unit-Test in
/// <c>FindSymbolToolTests.cs</c> beweist die Scanner-Logik; dieser Test beweist die
/// Wire-Propagierung durch den realen MCP-Subprozess.
///
/// A3-Pfad: wenn in <c>FindSymbolScanner.AppendMissHint</c> der Hint-Anhang deaktiviert
/// wird (z. B. <c>return baseText;</c> statt <c>return baseText + hint;</c>), dann
/// fehlen im Response die Markierungen <c>"Hinweis: kein C#-Symbol, aber Textfund"</c>
/// und die Datei-Liste. Der Test wuerde fehlschlagen.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class McpServerCommandMissHintTests
{
    [Fact]
    public async Task RunAsync_NonCsOnlyMatch_ReturnsExplicitMissHint()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        var text = await client.CallToolGetTextAsync(
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
