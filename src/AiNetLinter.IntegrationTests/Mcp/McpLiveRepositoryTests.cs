#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Live-Integrationstests fuer alle 10 MCP-Tools direkt gegen das eigene Repository.
/// Nutzt <see cref="RepositoryMcpHostFixture"/> zur geteilten MCP-Prozessverbindung pro Assembly.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpLiveRepositoryTests
{
    private readonly RepositoryMcpHostFixture _fixture;

    public McpLiveRepositoryTests(RepositoryMcpHostFixture fixture)
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
    public async Task LiveDogfood_GetCallTree_ReturnsTreeStructure()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["depth"] = 2,
                ["topN"] = 5,
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.DoesNotContain("WORKSPACE_DIAGNOSTIC", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_GetCallTreeMermaid_ReturnsFlowchartBlock()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["format"] = "mermaid",
            });

        Assert.NotNull(text);
        Assert.Contains("flowchart TD", text, StringComparison.Ordinal);
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
    public async Task LiveDogfood_MetricsTreeViolationDensity_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "metrics_tree",
            new Dictionary<string, object?>
            {
                ["root"] = null,
                ["mode"] = "violation_density",
                ["depth"] = 2,
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_MetricsTreeComplexity_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "metrics_tree",
            new Dictionary<string, object?>
            {
                ["root"] = null,
                ["mode"] = "complexity",
                ["depth"] = 2,
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_Safeguard_ReturnsResults()
    {
        var result = await _fixture.Client.CallToolAsync(
            "safeguard",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = null,
                ["minScore"] = 0.0,
                ["maxViolations"] = 20,
            });

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);

        var json = JsonSerializer.Deserialize<JsonObject>(
            result.StructuredContent!.Value.GetRawText())!;
        Assert.NotNull(json);

        Assert.True(json.ContainsKey("passed"));
        Assert.True(json.ContainsKey("score"));
        Assert.True(json.ContainsKey("threshold"));
        Assert.True(json.ContainsKey("violations"));
        Assert.True(json.ContainsKey("remediation"));
        Assert.True(json.ContainsKey("summary"));
        Assert.IsType<JsonArray>(json["violations"]);

        var score = (double)json["score"]!;
        Assert.True(score >= 5.0,
            $"Safeguard-Live-Score {score} unter Konzept-Korridor >= 5.0");
    }

    [Fact]
    public async Task LiveDogfood_PatternDetect_ReturnsStructuredResultsForAllSixPatterns()
    {
        var result = await _fixture.Client.CallToolAsync(
            "pattern_detect",
            new Dictionary<string, object?>
            {
                ["patterns"] = null,
                ["scopeFilter"] = null,
                ["maxResultsPerPattern"] = 20,
            });

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);

        var json = JsonSerializer.Deserialize<JsonObject>(
            result.StructuredContent!.Value.GetRawText())!;
        Assert.True(json.ContainsKey("patterns"));
        Assert.True(json.ContainsKey("summary"));

        var patterns = json["patterns"]!.AsArray();
        Assert.Equal(6, patterns.Count);
        var ids = patterns.Select(p => (string)p!["id"]!).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[] { "god-class", "async-void", "long-method", "public-without-doc", "empty-catch", "feature-envy" })
        {
            Assert.Contains(expected, ids);
        }
    }

    [Fact]
    public async Task LiveDogfood_DependencyGraph_ReturnsResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "dependency_graph",
            new Dictionary<string, object?>
            {
                ["filePath"] = "src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs",
                ["direction"] = "both",
                ["maxResults"] = 20,
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Ausgehende Abhaengigkeiten", text, StringComparison.Ordinal);
        Assert.Contains("Eingehende Abhaengigkeiten", text, StringComparison.Ordinal);
        Assert.DoesNotContain("WORKSPACE_DIAGNOSTIC", text, StringComparison.Ordinal);
    }
}
