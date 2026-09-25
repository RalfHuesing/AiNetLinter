#nullable enable

using System;
using System.IO;
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
        var targetPath = Path.Combine(repoRoot, "AiNetLinter.slnx");
        var resourceUri = $"ainetlinter://overview?targetPath={Uri.EscapeDataString(targetPath)}";
        var rulesResourceUri = $"ainetlinter://rules?targetPath={Uri.EscapeDataString(targetPath)}";
        var templates = await _fixture.Client.ListResourceTemplatesAsync();
        var resources = await _fixture.Client.ListResourcesAsync();
        var readResult = await _fixture.Client.ReadResourceAsync(resourceUri);
        var rulesReadResult = await _fixture.Client.ReadResourceAsync(rulesResourceUri);
        var guideResult = await _fixture.Client.ReadResourceAsync("ainetlinter://agent-guide");
        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(readResult.Contents));
        var rulesContent = Assert.IsType<TextResourceContents>(Assert.Single(rulesReadResult.Contents));
        var guideContent = Assert.IsType<TextResourceContents>(Assert.Single(guideResult.Contents));
        Assert.Contains(templates, template => template.UriTemplate == "ainetlinter://overview{?targetPath}");
        Assert.Contains(templates, template => template.UriTemplate == "ainetlinter://rules{?targetPath}");
        var guideResource = Assert.Single(resources);
        Assert.Equal("ainetlinter://agent-guide", guideResource.Uri);
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
        Assert.Contains("ainetlinter-rules.json", guideContent.Text, StringComparison.Ordinal);
        Assert.Contains(".agents/rules", guideContent.Text, StringComparison.Ordinal);
        Assert.Contains("Dauerhafte Agentenregel", guideContent.Text, StringComparison.Ordinal);
    }
}
