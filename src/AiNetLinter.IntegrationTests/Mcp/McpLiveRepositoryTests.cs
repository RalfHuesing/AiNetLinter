#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
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
    public async Task LiveDogfood_OverviewResourceRead_UsesEncodedRepositoryRoot()
    {
        var repoRoot = SolutionRootLocator.Find();
        var resourceUri = $"ainetlinter://overview?projectRoot={Uri.EscapeDataString(repoRoot)}";
        var templates = await _fixture.Client.ListResourceTemplatesAsync();
        var resources = await _fixture.Client.ListResourcesAsync();
        var tools = await _fixture.Client.ListToolsAsync();
        var readResult = await _fixture.Client.ReadResourceAsync(resourceUri);
        var content = Assert.Single(readResult.Contents);
        var textContent = Assert.IsType<TextResourceContents>(content);
        var expectedToolGroups = new[]
        {
            new[] { "find_symbol", "find_references", "get_call_tree", "get_impact", "get_type_hierarchy", "dependency_graph" },
            new[] { "get_symbol_body" },
            new[] { "get_namespace_tree", "get_class_structure", "get_file_skeleton", "get_index_scope", "get_hotspots" },
            new[] { "get_violations", "safeguard", "search_pattern", "metrics_tree", "metrics_lookup", "pattern_detect", "find_magic_values", "find_dead_code", "get_feature_context", "get_test_context" },
            new[] { "find_duplicates" },
            new[] { "reload_config", "get_server_health" }
        };

        Assert.Contains(templates, template => template.UriTemplate == "ainetlinter://overview{?projectRoot}");
        Assert.Empty(resources);
        Assert.Equal(25, tools.Count);
        Assert.Equal(
            expectedToolGroups.SelectMany(group => group).ToHashSet(StringComparer.Ordinal),
            tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(resourceUri, textContent.Uri);
        Assert.Equal("text/markdown", textContent.MimeType);
        Assert.Contains(repoRoot, textContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Solution:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Regeln:", textContent.Text, StringComparison.Ordinal);
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
                ["symbolIdentifier"] = "McpCodeGraphServer"
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
            $"Safeguard-Live-Score {score} unter Korridor >= 5.0");
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

    [Fact]
    public async Task LiveDogfood_GetHotspots_WithForwardSlashScopeFilter_ReturnsFilteredResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_hotspots",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.DoesNotContain("Keine Dateien im Scope", text, StringComparison.Ordinal);
        Assert.Contains("Gescannt:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_GetViolations_WithForwardSlashScopeFilter_ReturnsFilteredResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_violations",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.DoesNotContain("Keine Dateien im Scope", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_Safeguard_WithForwardSlashScopeFilter_AnalyzesMatchingClasses()
    {
        var result = await _fixture.Client.CallToolAsync(
            "safeguard",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp",
                ["minScore"] = 0.0,
                ["maxViolations"] = 20,
            });

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        var summary = (string)json["summary"]!;
        Assert.DoesNotContain(" 0 Klassen analysiert", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_FindDeadCode_WithForwardSlashScopeFilter_ReturnsResults()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_dead_code",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp",
                ["accessibility"] = "private_internal",
                ["confidence"] = "both",
                ["maxResults"] = 20
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.NotNull(json["summary"]);
        Assert.NotNull(json["deadSymbols"]);
    }

    [Fact]
    public async Task LiveDogfood_AuditDump_WritesReport()
    {
        var resultPrivateInternal = await _fixture.Client.CallToolGetTextAsync(
            "find_dead_code",
            new Dictionary<string, object?>
            {
                ["accessibility"] = "private_internal",
                ["confidence"] = "both",
                ["mode"] = "both",
                ["maxResults"] = 200
            });

        var resultDeadCodeMagic = await _fixture.Client.CallToolGetTextAsync(
            "find_magic_values",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "src/AiNetLinter/Mcp/Tools/DeadCode",
                ["maxResults"] = 100
            });

        var resultDeadCodeDuplicates = await _fixture.Client.CallToolGetTextAsync(
            "find_duplicates",
            new Dictionary<string, object?>
            {
                ["scopeFilter"] = "DeadCode",
                ["maxResults"] = 50
            });

        var outDir = Path.Combine(AppContext.BaseDirectory, "../../../../src/test-output");
        Directory.CreateDirectory(outDir);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== FIND_DEAD_CODE (private_internal, both) ===");
        sb.AppendLine(resultPrivateInternal);
        sb.AppendLine();
        sb.AppendLine("=== FIND_MAGIC_VALUES (DeadCode) ===");
        sb.AppendLine(resultDeadCodeMagic);
        sb.AppendLine();
        sb.AppendLine("=== FIND_DUPLICATES (DeadCode) ===");
        sb.AppendLine(resultDeadCodeDuplicates);
        File.WriteAllText(Path.Combine(outDir, "dead-code-audit.txt"), sb.ToString());

        Assert.Contains("# Dead-Code-Analyse", resultPrivateInternal, StringComparison.Ordinal);
        Assert.Contains("Magic-Value-Audit", resultDeadCodeMagic, StringComparison.Ordinal);
        Assert.Contains("Duplikat-Cluster", resultDeadCodeDuplicates, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_GetNamespaceTree_ReturnsProjectsAndNamespaces()
    {
        var overview = await _fixture.Client.CallToolGetTextAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>());

        Assert.NotNull(overview);
        Assert.Contains("# Solution Overview", overview, StringComparison.Ordinal);
        Assert.Contains("AiNetLinter", overview, StringComparison.Ordinal);

        var tree = await _fixture.Client.CallToolGetTextAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["project"] = "AiNetLinter",
                ["includeTypes"] = false
            });

        Assert.NotNull(tree);
        Assert.Contains("# Namespaces in Projekt 'AiNetLinter'", tree, StringComparison.Ordinal);
        Assert.Contains("AiNetLinter", tree, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveDogfood_FindDuplicates_StructuralMode_ReturnsValidSchema()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates",
            new Dictionary<string, object?>
            {
                ["mode"] = "structural",
                ["scopeDir"] = "src/AiNetLinter/Mcp/Tools/DeadCode",
                ["minTokens"] = 10,
                ["maxResults"] = 10,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);

        var json = JsonSerializer.Deserialize<JsonObject>(
            result.StructuredContent!.Value.GetRawText())!;
        Assert.True(json.ContainsKey("clusters"), "StructuredContent muss 'clusters' enthalten");
        Assert.True(json.ContainsKey("summary"), "StructuredContent muss 'summary' enthalten");
        Assert.IsType<JsonArray>(json["clusters"]);

        var summary = json["summary"]!.AsObject();
        Assert.True(summary.ContainsKey("mode"), "summary muss 'mode' enthalten");
        Assert.Equal("structural", (string?)summary["mode"]);
        Assert.True(summary.ContainsKey("methodsScanned"), "summary muss 'methodsScanned' enthalten");
        Assert.True((int?)summary["methodsScanned"] >= 0);
    }
}
