using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class GetIndexScopeToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetIndexScopeToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(null));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsCsCountMarkedAsGraphCovered()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(".cs: 5 Dateien (voll vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsJsRazorCssCountsViaWebFileCatalog()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(".css: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(".js: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(".razor: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsXamlAndHtmlCountsMarkedAsNotGraphCovered()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(_fixture.Catalog));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(".xaml: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(".html: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromXamlHtmlCount()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var projectDir = Path.GetDirectoryName(fixture.GreeterPath)!;
        var generatedDir = Path.Combine(projectDir, "obj", "Debug");
        Directory.CreateDirectory(generatedDir);
        File.WriteAllText(Path.Combine(generatedDir, "Generated.xaml"), "<Page />");
        File.WriteAllText(Path.Combine(generatedDir, "Generated.html"), "<html></html>");

        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(".xaml: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(".html: 1 Dateien (nicht vom Symbolgraph abgedeckt)", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Matches(@"\b\d+\s+Dateien?\s+haben\s+Compile-Fehler", text);
    }
}
