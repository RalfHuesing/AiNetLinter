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
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "LinterEngine" } });
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
                ["namePatterns"] = new[] { "Get" },
                ["maxResults"] = 1,
            });
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Treffer gesamt", text, StringComparison.Ordinal);
        Assert.Contains("gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentApi_DescribesCsharpOnlyToolScopeWithoutHardcodedCounts()
    {
        var docPath = Path.Combine(SolutionRootLocator.Find(), "Docs", "agent-api.md");

        Assert.True(File.Exists(docPath),
            $"Doku-Datei nicht gefunden unter '{docPath}'. Bitte Pfad-Aufloesung pruefen.");

        var docText = File.ReadAllText(docPath);

        Assert.Contains("denselben zentralen `ServerInstructions`-Text", docText, StringComparison.Ordinal);
        Assert.Contains("C#-Symbolgraph-Grenze", docText, StringComparison.Ordinal);
        Assert.Contains("`tools/list`", docText, StringComparison.Ordinal);
        Assert.Contains("`ainetlinter://overview`", docText, StringComparison.Ordinal);
        Assert.Contains("| `get_index_scope` |", docText, StringComparison.Ordinal);
        Assert.Contains("| `get_hotspots` |", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("alle Tools sind C#-only", docText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("| `search_pattern` |", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("search_pattern nutzt auch Nicht-C#-Dateien", docText, StringComparison.Ordinal);
        Assert.Contains("enrichCSharp", docText, StringComparison.Ordinal);
        Assert.Contains("ambiguous", docText, StringComparison.Ordinal);
        Assert.Contains("unavailable", docText, StringComparison.Ordinal);
        Assert.Contains("Tool-Annotations", docText, StringComparison.Ordinal);
        Assert.Contains("keine Sicherheitsgarantie", docText, StringComparison.Ordinal);
        Assert.Contains("\"targetPath\": \"C:\\\\Projects\\\\MyApp\"", docText, StringComparison.Ordinal);
        Assert.Contains("bodyAvailability", docText, StringComparison.Ordinal);
        Assert.Contains("contentMode", docText, StringComparison.Ordinal);
        Assert.Contains("minLinePercentage", docText, StringComparison.Ordinal);
        Assert.Contains("Progressive Disclosure", docText, StringComparison.Ordinal);
        Assert.Contains("`includeSessions=true`", docText, StringComparison.Ordinal);
        Assert.Contains("`maxSessions`", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("ohne Target getrennte Projekt-/Assembly-Session-Listen", docText, StringComparison.Ordinal);

        var getImpactStart = docText.IndexOf(
            "**`get_impact` (Symbol-Branch) — Assembly-Vertrag:**", StringComparison.Ordinal);
        Assert.True(getImpactStart >= 0,
            "Der getrennte Assembly-Vertrag für get_impact fehlt.");
        var getImpactEnd = docText.IndexOf(
            "**`get_impact` (`detailLevel=change-context`)", getImpactStart, StringComparison.Ordinal);
        Assert.True(getImpactEnd > getImpactStart,
            "Der getrennte Assembly-Vertrag für get_impact ist nicht begrenzt.");
        var getImpactAssemblySection = docText.Substring(getImpactStart, getImpactEnd - getImpactStart);
        Assert.Contains("keinen `includeReferences`-Parameter", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.Contains("ExpandAssemblyReferences=true", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.Contains("callSites", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.Contains("analysis", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.DoesNotContain("includeReferences=false", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.DoesNotContain("includeReferences=true", getImpactAssemblySection, StringComparison.Ordinal);
        Assert.Contains("nicht als `get_impact`-Antwortvertrag", getImpactAssemblySection, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationGuide_SeparatesFindReferencesAndGetImpactAssemblyOptions()
    {
        var docPath = Path.Combine(SolutionRootLocator.Find(), "Docs", "integration.md");

        Assert.True(File.Exists(docPath),
            $"Doku-Datei nicht gefunden unter '{docPath}'. Bitte Pfad-Aufloesung pruefen.");

        var docText = File.ReadAllText(docPath);
        var findReferencesStart = docText.IndexOf(
            "- Methoden-Aufrufer finden", StringComparison.Ordinal);
        var getImpactStart = docText.IndexOf(
            "- Impact eines Symbols prüfen", StringComparison.Ordinal);
        var nextBulletStart = getImpactStart < 0
            ? -1
            : docText.IndexOf("- Treffer semantisch einordnen", getImpactStart, StringComparison.Ordinal);

        Assert.True(findReferencesStart >= 0,
            "Die find_references-Empfehlung im Integrationsleitfaden fehlt.");
        Assert.True(getImpactStart > findReferencesStart,
            "Die get_impact-Empfehlung ist nicht von der find_references-Empfehlung getrennt.");
        Assert.True(nextBulletStart > getImpactStart,
            "Die get_impact-Empfehlung ist nicht bis zum nächsten Tool begrenzt.");

        var findReferencesSection = docText.Substring(findReferencesStart, getImpactStart - findReferencesStart);
        var getImpactSection = docText.Substring(getImpactStart, nextBulletStart - getImpactStart);

        Assert.Contains("`find_references(symbolIdentifier: \"MyClass.MyMethod\", depth: 2)`", findReferencesSection, StringComparison.Ordinal);
        Assert.Contains("`includeReferences: true`", findReferencesSection, StringComparison.Ordinal);
        Assert.DoesNotContain("`get_impact`", findReferencesSection, StringComparison.Ordinal);
        Assert.Contains("`get_impact(symbolIdentifier: ..., depth: 2)`", getImpactSection, StringComparison.Ordinal);
        Assert.Contains("ausschließlich `symbolIdentifier`", getImpactSection, StringComparison.Ordinal);
        Assert.Contains("Referenzexpansion ist intern festgelegt und nicht öffentlich wählbar", getImpactSection, StringComparison.Ordinal);
        Assert.DoesNotContain("includeReferences", getImpactSection, StringComparison.Ordinal);
    }
}
