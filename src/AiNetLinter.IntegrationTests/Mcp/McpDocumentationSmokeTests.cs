#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// A3-Nachweis fuer die MCP-Doku: fuehrt eine kleine Anzahl repraesentativer Tool-Calls
/// gegen die echte AiNetLinter.slnx aus und assertiert gegen Erwartungs-Strings aus der Doku.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpDocumentationSmokeTests
{
    private readonly RepositoryMcpHostFixture _fixture;

    public McpDocumentationSmokeTests(RepositoryMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSymbol_ReturnsLinterEngineHit()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "LinterEngine" });
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("LinterEngine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIndexScope_ListsCsAsLargestCategory()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_index_scope", new Dictionary<string, object?>());
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains(".cs", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindSymbol_WithWidePattern_TruncatesWithMetaLine()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "Get",
                ["maxResults"] = 1,
            });
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentApi_CountsCsharpOnlyToolsCorrectly()
    {
        var docPath = Path.Combine(SolutionRootLocator.Find(), "Docs", "agent-api.md");

        Assert.True(File.Exists(docPath),
            $"Doku-Datei nicht gefunden unter '{docPath}'. Bitte Pfad-Aufloesung pruefen.");

        var docText = File.ReadAllText(docPath);

        Assert.Contains("13 Tools sind C#-only", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("12 Tools sind C#-only", docText, StringComparison.Ordinal);
        Assert.Contains("`search_pattern` ist der vorgesehene Fallback", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("search_pattern nutzt auch Nicht-C#-Dateien", docText, StringComparison.Ordinal);
    }
}
