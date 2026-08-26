#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Live-Integrationstests fuer die MCP-Resources direkt gegen das eigene Repository.
/// </summary>
[Trait("Category", "Dogfood")]
public sealed class McpLiveRepositoryResourceTests
{
    private readonly RepositoryMcpHostFixture _fixture;

    public McpLiveRepositoryResourceTests(RepositoryMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LiveDogfood_OverviewResourceRead_UsesEncodedRepositoryRoot()
    {
        var repoRoot = SolutionRootLocator.Find();
        var resourceUri = $"ainetlinter://overview?projectRoot={Uri.EscapeDataString(repoRoot)}";
        var rulesResourceUri = $"ainetlinter://rules?projectRoot={Uri.EscapeDataString(repoRoot)}";
        var templates = await _fixture.Client.ListResourceTemplatesAsync();
        var resources = await _fixture.Client.ListResourcesAsync();
        var tools = await _fixture.Client.ListToolsAsync();
        var readResult = await _fixture.Client.ReadResourceAsync(resourceUri);
        var rulesReadResult = await _fixture.Client.ReadResourceAsync(rulesResourceUri);
        var guideResult = await _fixture.Client.ReadResourceAsync("ainetlinter://agent-guide");
        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(readResult.Contents));
        var rulesContent = Assert.IsType<TextResourceContents>(Assert.Single(rulesReadResult.Contents));
        var guideContent = Assert.IsType<TextResourceContents>(Assert.Single(guideResult.Contents));
        var expectedToolGroups = new[]
        {
            new[] { "find_symbol", "find_references", "get_call_tree", "get_impact", "get_type_hierarchy", "dependency_graph" },
            new[] { "get_symbol_body" },
            new[] { "get_namespace_tree", "get_class_structure", "get_file_skeleton", "get_index_scope", "get_hotspots" },
            new[] { "get_violations", "safeguard", "search_pattern", "metrics_tree", "metrics_lookup", "pattern_detect", "find_magic_values", "find_dead_code", "get_feature_context", "get_test_context" },
            new[] { "find_duplicates", "inspect_assembly", "find_assembly_extensions" },
            new[] { "reload_config", "get_server_health", "report_observability_feedback" }
        };

        Assert.Contains(templates, template => template.UriTemplate == "ainetlinter://overview{?projectRoot}");
        Assert.Contains(templates, template => template.UriTemplate == "ainetlinter://rules{?projectRoot}");
        var guideResource = Assert.Single(resources);
        Assert.Equal("ainetlinter://agent-guide", guideResource.Uri);
        Assert.Equal(28, tools.Count);
        Assert.Equal(expectedToolGroups.SelectMany(group => group).ToHashSet(StringComparer.Ordinal), tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(resourceUri, textContent.Uri);
        Assert.Equal("text/markdown", textContent.MimeType);
        Assert.Contains(repoRoot, textContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Solution:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Regeln:", textContent.Text, StringComparison.Ordinal);
        Assert.Equal(rulesResourceUri, rulesContent.Uri);
        Assert.Equal("text/markdown", rulesContent.MimeType);
        Assert.Contains("# AiNetLinter — effektive Regelkonfiguration", rulesContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Konfigurationsquelle:", rulesContent.Text, StringComparison.Ordinal);
        Assert.Contains("## Aktive Regeln", rulesContent.Text, StringComparison.Ordinal);
        Assert.Contains("## Effektive Schwellwerte", rulesContent.Text, StringComparison.Ordinal);
        Assert.Contains("| `MaxLineCount` | 500 | aktiv |", rulesContent.Text, StringComparison.Ordinal);
        Assert.Equal("ainetlinter://agent-guide", guideContent.Uri);
        Assert.Contains("AiNetLinter MCP-Bootstrap", guideContent.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter.project.json", guideContent.Text, StringComparison.Ordinal);
        Assert.Contains(".agents/rules", guideContent.Text, StringComparison.Ordinal);
        Assert.Contains("Dauerhafte Agentenregel", guideContent.Text, StringComparison.Ordinal);
    }
}
