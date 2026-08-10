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
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".cs", 5, "(voll vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_StructuredContentDeserializesToFileTypeBreakdownEntries()
    {
        // S1.3: StructuredContent ergaenzt den Text additiv — dieselben Zaehlwerte wie die
        // Text-Zeile ".cs: 5 Dateien (voll vom Symbolgraph abgedeckt)".
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = JsonSerializer.Deserialize<List<FileTypeBreakdownEntry>>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(entries);
        var csEntry = entries!.Single(e => e.Extension == ".cs");
        Assert.Equal(5, csEntry.Count);
        Assert.True(csEntry.SymbolGraphCovered);
        var cssEntry = entries.Single(e => e.Extension == ".css");
        Assert.Equal(1, cssEntry.Count);
        Assert.False(cssEntry.SymbolGraphCovered);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsJsRazorCssCountsViaWebFileCatalog()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".css", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".js", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".razor", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MixedFixture_ReturnsXamlAndHtmlCountsMarkedAsNotGraphCovered()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".xaml", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".html", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(BuildBreakdownLine(".xaml", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
        Assert.Contains(BuildBreakdownLine(".html", 1, "(nicht vom Symbolgraph abgedeckt)"), textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithPluralAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        CompileErrorHeaderAssertions.AssertStartsWithCompileErrorHeader(text, expectedFileCount: 3);
    }

    [Fact]
    public async Task ExecuteAsync_SingleCompileErrorFixture_OutputStartsWithSingularAggregateWarning()
    {
        using var fixture = new SingleCompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        CompileErrorHeaderAssertions.AssertStartsWithCompileErrorHeader(text, expectedFileCount: 1);
    }

    /// <summary>
    /// Singular/Plural an einer Stelle gehalten, damit jede Datei-Typ-Zeile konsistent zur
    /// tatsaechlichen Engine-Ausgabe prueft (count == 1 -> "Datei", sonst "Dateien").
    /// </summary>
    private static string BuildBreakdownLine(string extension, int count, string suffix)
    {
        var label = count == 1 ? "Datei" : "Dateien";
        return $"{extension}: {count} {label} {suffix}";
    }
}
