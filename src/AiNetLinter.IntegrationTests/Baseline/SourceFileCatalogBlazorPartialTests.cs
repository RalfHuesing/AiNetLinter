#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class SourceFileCatalogBlazorPartialTests
{
    [Fact]
    public async Task FindSymbol_BlazorPartialFixture_FindsRazorGeneratedComponentBaseAndNoCompileError()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await FindSymbolTool.ExecuteAsync(
            state, ["SiteView"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.Contains("SiteView", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.Contains(".cs: 1 Datei (voll vom Symbolgraph abgedeckt)", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseAndNoCompileError()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/BlazorPartialMini/SiteView.razor.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("CS0115", text, System.StringComparison.Ordinal);
        Assert.Contains("ComponentBase", text, System.StringComparison.Ordinal);
    }
}
