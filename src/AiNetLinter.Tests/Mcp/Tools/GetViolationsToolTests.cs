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

public sealed class GetViolationsToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetViolationsToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(null);

        var result = await GetViolationsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture()
    {
        using var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetViolationsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Lint-Violations:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_RestrictsViolations()
    {
        using var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage()
    {
        using var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetViolationsTool.ExecuteAsync(state, "DoesNotExistAnywhere", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable()
    {
        using var state = new McpCodeGraphServer(_fixture.Catalog);

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("| Datei | Zeile | Regel | Details |", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_DoesNotIncludeCompileErrorsAsViolations()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await GetViolationsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("CS1513", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0246", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis:", text, StringComparison.Ordinal);
    }
}
