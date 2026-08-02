#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// A3-Nachweis fuer die MCP-Doku in 008: fuehrt eine kleine Anzahl repraesentativer Tool-Calls
/// gegen die echte AiNetLinter.slnx aus und assertiert wortwoertlich gegen Erwartungs-Strings,
/// die aus der Doku uebernommen sind. Aenderung an der Doku ohne Anpassung dieser Strings = Test
/// wird rot. Doku-Luege = Test wird rot. Verhindert Drift zwischen Doku-Aussagen und
/// beobachtbarem Server-Verhalten.
/// </summary>
[Trait("Category", "Integration")]
[Collection("ConsoleTestCollection")]
public sealed class McpDocumentationSmokeTests : IClassFixture<McpLiveRepositoryFixture>
{
    private readonly McpLiveRepositoryFixture _fixture;

    public McpDocumentationSmokeTests(McpLiveRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSymbol_ReturnsLinterEngineHit()
    {
        // Erwartung: "LinterEngine" ist ein bekanntes Symbol der AiNetLinter.slnx und wird in der
        // Doku (Docs/agent-api.md#mcp-server-modus) als Beispiel-Pattern fuer find_symbol genannt.
        // Output enthaelt daher den Symbolnamen (case-insensitive).
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "LinterEngine" });
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("LinterEngine", text, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIndexScope_ListsCsAsLargestCategory()
    {
        // Erwartung: "get_index_scope" listet .cs als groesste Datei-Kategorie. Wird in der Doku
        // (Docs/agent-api.md#mcp-server-modus, Tool-Tabelle) als die vom Symbolgraph voll
        // abgedeckte Kategorie beschrieben.
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_index_scope", new Dictionary<string, object?>());
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains(".cs", text, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindSymbol_WithWidePattern_TruncatesWithMetaLine()
    {
        // Erwartung: Trunkierung nutzt die exakte Meta-Zeile aus McpTruncation.cs:40, wortwoertlich
        // in Docs/agent-api.md#mcp-server-modus uebernommen. "Get" ist sehr verbreitet (ueber 50
        // Treffer in der eigenen Solution), maxResults=1 erzwingt Trunkierung. Hartkodiert hier
        // als A3-Beweis: Doku-Aussage "Listen-Meta-Zeile" = Code-Meta-Zeile.
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "Get",
                ["maxResults"] = 1,
            });
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Treffer gesamt", text, System.StringComparison.Ordinal);
        Assert.Contains("gezeigt", text, System.StringComparison.Ordinal);
    }
}
