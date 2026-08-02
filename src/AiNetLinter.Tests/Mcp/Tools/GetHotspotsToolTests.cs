using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class GetHotspotsToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetHotspotsToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(null);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SmallMaxLineCount_MarksFileAsCritical()
    {
        var state = new McpCodeGraphServer(_fixture.Catalog, maxLineCount: 1);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Kritische Dateien", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MidRangeMaxLineCount_MarksFileAsWarning()
    {
        var state = new McpCodeGraphServer(_fixture.Catalog, maxLineCount: 7);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Warnungs-Dateien", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultMaxLineCount_AllFilesGreen()
    {
        var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("im gruenen Bereich", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles()
    {
        var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetHotspotsTool.ExecuteAsync(state, "SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Gescannt: 5 .cs-Dateien", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage()
    {
        var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetHotspotsTool.ExecuteAsync(state, "DoesNotExistAnywhere", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("im gruenen Bereich", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Matches(@"\b\d+\s+Dateien?\s+haben\s+Compile-Fehler", text);
    }
}
