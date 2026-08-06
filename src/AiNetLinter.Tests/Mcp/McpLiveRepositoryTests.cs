#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Live-Integrationstests fuer alle 9 MCP-Tools direkt gegen das eigene Repository.
/// Ersetzt ad-hoc Python-Dogfooding-Skripte durch saubere, automatisierte C# xUnit-Tests.
/// Nutzt <see cref="McpLiveRepositoryFixture"/> zur einmaligen MCP-Prozessverbindung pro Testklasse.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpLiveRepositoryTests : IClassFixture<McpLiveRepositoryFixture>
{
    private readonly McpLiveRepositoryFixture _fixture;

    public McpLiveRepositoryTests(McpLiveRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LiveDogfood_FindSymbol_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("LinterEngine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_FindReferences_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_references",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetImpact_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetTypeHierarchy_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?>
            {
                ["typeIdentifier"] = "McpCodeGraphServer"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Basisklassen", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetFileSkeleton_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_file_skeleton",
            new Dictionary<string, object?>
            {
                ["filePath"] = "src/AiNetLinter/Program.cs"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Program", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetIndexScope_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_index_scope");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains(".cs", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetHotspots_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_hotspots");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetViolations_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_violations");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_SearchPattern_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "AiNetLinter",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("AiNetLinter", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_Safeguard_ReturnsResults()
    {
        // End-to-end-Verifikation: das safeguard-Tool liefert auf dem echten
        // AiNetLinter-Repo einen Score >= 5.0 und einen gueltigen JSON-Schema-2020-12-
        // Structured-Content. Score-Aufruf gegen den Live-Subprozess via _fixture.Client
        // (geteilter MCP-Server pro Testklasse, startet einmal in IAsyncLifetime).
        // minScore wird bewusst auf 0.0 gesetzt, damit der Korridor-Assert die
        // Score-Berechnung isoliert prueft, ohne die Passed-Logik des Tools mit dem
        // Korridor zu vermischen.
        var result = await _fixture.Client.CallToolAsync(
            "safeguard",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = null,
                ["minScore"] = 0.0,
                ["maxViolations"] = 20,
            });

        // Tool-Layer-Invariante: kein Malfunction-/Loading-/SolutionNotLoaded-Fehler
        // auf einem geladenen Live-Repo. Fixture garantiert Load via IAsyncLifetime
        // plus 60s Timeout plus Retry-Schleife (siehe McpLiveRepositoryFixture).
        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);

        // StructuredContent ist JsonElement?; Deserialisierung zur JsonObject-Form
        // folgt dem Pattern aus SafeguardToolTests.
        var json = JsonSerializer.Deserialize<JsonObject>(
            result.StructuredContent!.Value.GetRawText())!;
        Assert.NotNull(json);

        // Pflicht-Felder gemaess konzept.md (JSON-Schema 2020-12 Vertrag): passed,
        // score, threshold, violations, remediation, summary. Nur Existenz und Typ
        // werden geprueft; konkrete Werte separat.
        Assert.True(json.ContainsKey("passed"));
        Assert.True(json.ContainsKey("score"));
        Assert.True(json.ContainsKey("threshold"));
        Assert.True(json.ContainsKey("violations"));
        Assert.True(json.ContainsKey("remediation"));
        Assert.True(json.ContainsKey("summary"));
        Assert.IsType<JsonArray>(json["violations"]);

        // Korridor-Assert: score >= 5.0. Real gemessener Wert auf dem AiNetLinter-
        // Repo: 10.00/10 (deutlich ueber dem Konzept-Korridor). Bei Verletzung
        // dieser Schwelle liegt der Bug in der Score-Formel (EPIC-01-Scope),
        // nicht im Tool-Layer — dann ist blocked mit Verweis auf SafeguardScanner
        // zu setzen, nicht der Schwellwert im Test anzupassen.
        var score = (double)json["score"]!;
        Assert.True(score >= 5.0,
            $"Safeguard-Live-Score {score} unter Konzept-Korridor >= 5.0 — " +
            "moeglicher Bug in der Score-Formel (EPIC-01-Scope), nicht im step-003 zu fixen.");
    }
}
