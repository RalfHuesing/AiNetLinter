using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class GetIndexScopeToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetIndexScopeToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsCsCountMarkedAsGraphCovered()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".cs", 5, "(voll vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Population:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Roslyn-Dokumente", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_StructuredContentDeserializesToFileTypeBreakdownEntries()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("breakdown")
            .Deserialize<List<FileTypeBreakdownEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        var csEntry = entries!.Single(e => e.Extension == ".cs");
        Assert.Equal(5, csEntry.Count);
        Assert.True(csEntry.SymbolGraphCovered);
        var cssEntry = entries.Single(e => e.Extension == ".css");
        Assert.Equal(1, cssEntry.Count);
        Assert.False(cssEntry.SymbolGraphCovered);
        Assert.Equal("find_symbol", csEntry.RoutingTool);
        Assert.Equal("pattern", csEntry.QueryField);
        Assert.Equal("search_pattern", cssEntry.RoutingTool);
        Assert.Equal("pattern", cssEntry.QueryField);
        Assert.Equal("all", cssEntry.ScopeType);
        Assert.Equal("**/*.css", cssEntry.FileFilter);

        var routing = result.StructuredContent.Value.GetProperty("routing");
        Assert.Equal("find_symbol", routing.GetProperty("cSharp").GetProperty("tool").GetString());
        Assert.Equal("search_pattern", routing.GetProperty("nonCSharp").GetProperty("tool").GetString());

        var population = result.StructuredContent.Value.GetProperty("population");
        Assert.True(population.GetProperty("physicalFileCount").GetInt32() >= entries.Sum(entry => entry.Count));
        Assert.True(population.GetProperty("roslynDocumentCount").GetInt32() >= 5);
        Assert.True(population.GetProperty("testDocumentCount").GetInt32() >= 0);
        Assert.True(population.GetProperty("generatedDocumentCount").GetInt32() >= 0);
        Assert.Equal(entries.Sum(entry => entry.Count), population.GetProperty("shownPhysicalFileCount").GetInt32());
        Assert.False(population.TryGetProperty("shownCount", out _));
        Assert.False(population.TryGetProperty("excludedCount", out _));
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsJsRazorCssCountsViaWebFileCatalog()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".css", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".js", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".razor", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains("routing=search_pattern(pattern, scopeType=all, includePatterns=**/*.razor)", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsXamlAndHtmlCountsMarkedAsNotGraphCovered()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".xaml", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".html", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_DiscoversAdditionalExtensionsAndOmitsZeroEntries()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("breakdown")
            .Deserialize<List<FileTypeBreakdownEntry>>(McpJsonOptions.Default)!;
        Assert.Contains(entries, entry => entry.Extension == ".json" && entry.Count == 1 && !entry.SymbolGraphCovered);
        Assert.Contains(entries, entry => entry.Extension == ".md" && entry.Count == 1 && !entry.SymbolGraphCovered);
        Assert.Contains(entries, entry => entry.Extension == ".slnx" && entry.Count == 1 && !entry.SymbolGraphCovered);
        Assert.DoesNotContain(entries, entry => entry.Extension == ".ts");
        Assert.DoesNotContain(entries, entry => entry.Count == 0);
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

        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".xaml", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".html", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsBreakdownWithoutCompileErrorHint()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    private static string BuildBreakdownLine(string extension, int count, string suffix)
    {
        var label = count == 1 ? "Datei" : "Dateien";
        return $"{extension}: {count} {label} {suffix}";
    }
}
