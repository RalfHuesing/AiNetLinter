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
    public void ToolsDocumentation_CoversAllTargetBoundToolsAndContracts()
    {
        var docPath = Path.Combine(SolutionRootLocator.Find(), "Docs", "mcp", "tools.md");
        Assert.True(File.Exists(docPath), $"Doku-Datei nicht gefunden unter '{docPath}'.");

        var docText = File.ReadAllText(docPath);

        // Verweist auf tools/list als massgebliche Eingabespezifikation
        Assert.Contains("`tools/list`", docText, StringComparison.Ordinal);

        // Negative Schutzregeln: Veraltete / verbotene Protokoll-Artefakte duerfen nicht dokumentiert sein
        Assert.DoesNotContain("structuredContent", docText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operationStatus", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("`capabilities`", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("`handoff=true`", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("Additive Handoff-Payloads", docText, StringComparison.Ordinal);

        // Alle 28 zielgebundenen Tools muessen in der Uebersicht dokumentiert sein
        var targetBoundTools = new[]
        {
            "get_file_tree", "get_namespace_tree", "inspect_assembly", "find_assembly_extensions",
            "get_assembly_context", "search_assembly", "resolve_type_origin", "find_implementations",
            "find_symbol", "find_references", "get_call_tree", "get_impact", "get_type_hierarchy",
            "dependency_graph", "get_file_skeleton", "get_class_structure", "get_index_scope",
            "get_hotspots", "metrics_tree", "metrics_lookup", "get_feature_context", "get_test_context",
            "verify", "pattern_detect", "get_symbol_body", "search_pattern", "reload_config",
            "find_duplicates",
        };

        foreach (var toolName in targetBoundTools)
        {
            Assert.Contains($"`{toolName}`", docText, StringComparison.Ordinal);
        }

        Assert.Contains("`get_server_health`", docText, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationGuide_UsesPublishedToolsAndFinalDiscoveryContract()
    {
        var docPath = Path.Combine(SolutionRootLocator.Find(), "Docs", "mcp", "integration.md");
        Assert.True(File.Exists(docPath), $"Doku-Datei nicht gefunden unter '{docPath}'.");

        var docText = File.ReadAllText(docPath);

        Assert.Contains("`ainetlinter://agent-guide`", docText, StringComparison.Ordinal);
        Assert.Contains("`tools/list`", docText, StringComparison.Ordinal);
        Assert.Contains("`ainetlinter://overview?targetPath=", docText, StringComparison.Ordinal);
        Assert.Contains("`ainetlinter://rules?targetPath=", docText, StringComparison.Ordinal);
        Assert.Contains("`get_server_health`", docText, StringComparison.Ordinal);

        // Negative Schutzregeln
        Assert.DoesNotContain("McpPayloadMeasurement", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("Token-Schätzungen", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("structuredContent", docText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operationStatus", docText, StringComparison.Ordinal);
        Assert.DoesNotContain("`capabilities`", docText, StringComparison.Ordinal);
    }
}
