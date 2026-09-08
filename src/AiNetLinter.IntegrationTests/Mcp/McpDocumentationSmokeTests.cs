#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Assert.Contains("\"targetPath\": \"C:\\\\Projects\\\\MyApp\\\\MyApp.slnx\"", docText, StringComparison.Ordinal);
        Assert.Contains("bodyAvailability", docText, StringComparison.Ordinal);
        Assert.Contains("contentMode", docText, StringComparison.Ordinal);
        Assert.Contains("minLinePercentage", docText, StringComparison.Ordinal);
        Assert.Contains("Progressive Disclosure", docText, StringComparison.Ordinal);
        Assert.Contains("`includeSessions=true`", docText, StringComparison.Ordinal);
        Assert.Contains("`maxSessions`", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("ohne Target getrennte Projekt-/Assembly-Session-Listen", docText, StringComparison.Ordinal);

        var normalizedDocText = docText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var matrixStart = normalizedDocText.IndexOf("| Tool | Input | Output |", StringComparison.Ordinal);
        Assert.True(matrixStart >= 0, "Die MCP-Tool-Matrix fehlt in der Agent-API-Dokumentation.");
        var matrixEnd = normalizedDocText.IndexOf("\n\n", matrixStart, StringComparison.Ordinal);
        Assert.True(matrixEnd > matrixStart, "Das Ende der MCP-Tool-Matrix wurde nicht gefunden.");
        var matrix = normalizedDocText.Substring(matrixStart, matrixEnd - matrixStart);
        var targetBoundTools = new[]
        {
            "get_file_tree", "get_namespace_tree", "inspect_assembly", "find_assembly_extensions",
            "get_assembly_context", "search_assembly", "resolve_type_origin", "find_implementations",
            "find_symbol", "find_references", "get_call_tree", "get_impact", "get_type_hierarchy",
            "dependency_graph", "get_file_skeleton", "get_class_structure", "get_index_scope",
            "get_hotspots", "metrics_tree", "metrics_lookup", "get_feature_context", "get_test_context",
            "get_violations", "safeguard", "pattern_detect", "find_magic_values", "find_dead_code",
            "get_symbol_body", "search_pattern", "reload_config", "find_duplicates",
        };

        foreach (var toolName in targetBoundTools)
        {
            var row = matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(candidate => candidate.StartsWith($"| `{toolName}` |", StringComparison.Ordinal));
            Assert.NotNull(row);
            var columns = row!.Split('|');
            Assert.True(columns.Length >= 4, $"Ungültige Matrixzeile für {toolName}.");
            Assert.Contains("targetPath", columns[2], StringComparison.Ordinal);
            Assert.Contains("Pflicht", columns[2], StringComparison.Ordinal);
        }

        var globalHealthRow = matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(candidate => candidate.StartsWith("| `get_server_health` |", StringComparison.Ordinal));
        Assert.Contains("targetPath?", globalHealthRow, StringComparison.Ordinal);
        var feedbackRow = matrix.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(candidate => candidate.StartsWith("| `report_observability_feedback` |", StringComparison.Ordinal));
        Assert.DoesNotContain("targetPath", feedbackRow, StringComparison.Ordinal);

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
