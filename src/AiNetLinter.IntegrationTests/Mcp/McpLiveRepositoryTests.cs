#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Live-Integrationstests fuer die MCP-Tools direkt gegen das eigene Repository.
/// Nutzt <see cref="RepositoryMcpHostFixture"/> zur geteilten MCP-Prozessverbindung pro Assembly.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed partial class McpLiveRepositoryTests
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
                ["namePatterns"] = new[] { "LinterEngine" },
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
    public async Task LiveDogfood_GetFeatureContext_ReturnsTextStructuredParity()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_feature_context",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "FeatureContextScanner.ScanAsync",
                ["maxCallers"] = 5,
                ["maxTests"] = 5,
            });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        var declaration = json["declaration"]!.AsObject();
        Assert.Contains((string)declaration["name"]!, text, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "callers", "completeness", "declaration", "isTruncated", "metrics", "metricsStatus", "nextStep", "testContext", "truncatedBy", "violations", "wireBudget" },
            json
                .Where(property => !string.Equals(property.Key, "navigation", StringComparison.Ordinal))
                .Select(property => property.Key)
                .OrderBy(key => key, StringComparer.Ordinal));
        Assert.NotNull(json["navigation"]);

        Assert.NotNull(json["callers"]);
        Assert.NotNull(json["testContext"]);
        Assert.NotNull(json["metrics"]);
        Assert.NotNull(json["violations"]);
        Assert.NotNull(json["completeness"]);

        var violations = json["violations"]!.AsObject();
        var status = (string)violations["status"]!;
        var totalViolations = (int)violations["totalViolationsOnFile"]!;
        Assert.Contains($"Status: {status}", text, StringComparison.Ordinal);
        Assert.Contains($"({totalViolations} Verstoesse", text, StringComparison.Ordinal);
        Assert.False(json.ContainsKey("degraded"));
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
                ["filePaths"] = new[] { "src/AiNetLinter/Program.cs" }
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

        Assert.True(json.ContainsKey("threshold"));
        Assert.True(json.ContainsKey("violations"));
        Assert.True(json.ContainsKey("totalViolationCount"));
        Assert.True(json.ContainsKey("shownViolationCount"));
        Assert.True(json.ContainsKey("violationsTruncated"));
        Assert.True(json.ContainsKey("remediation"));
        Assert.True(json.ContainsKey("summary"));
        Assert.True(json.ContainsKey("scope"));
        Assert.True(json.ContainsKey("excludedDocumentCount"));
        Assert.True((bool)json["scoreIsNotScope"]!);
        Assert.IsType<JsonArray>(json["violations"]);
        Assert.True(json.ContainsKey("score"),
            "Bewusst ausgeschlossene/generated Dokumente dürfen den entscheidbaren Solution-Score nicht entfernen.");
        Assert.True(json.ContainsKey("passed"));
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
        Assert.NotEqual("not_decidable", (string)json["summary"]!["completeness"]!);
        Assert.All(patterns, pattern =>
            Assert.NotEqual("not_decidable", (string)pattern!["status"]!));
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
        Assert.NotNull(json["candidates"]);
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
