#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class GetHotspotsToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetHotspotsToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SmallMaxLineCount_MarksFileAsCritical()
    {
        var state = _fixture.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Kritische Dateien", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SmallMaxLineCount_StructuredContentDeserializesToHotspotEntries()
    {
        // StructuredContent ergaenzt den Text additiv — Category spiegelt dieselbe
        // Schwellwert-Klassifizierung wie die Text-Sektion "Kritische Dateien".
        var state = _fixture.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("hotspots")
            .Deserialize<List<HotspotEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        var greeter = entries!.Single(e => e.RelativePath.Contains("Greeter.cs", StringComparison.Ordinal));
        Assert.Equal("critical", greeter.Category);
    }

    [Fact]
    public async Task ExecuteAsync_MidRangeMaxLineCount_MarksFileAsWarning()
    {
        var state = _fixture.CreateServer(7);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Warnungs-Dateien", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultMaxLineCount_AllFilesGreen()
    {
        var state = _fixture.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("im gruenen Bereich", textContent.Text, StringComparison.Ordinal);
        // Regression: "ok"-Dateien duerfen nicht in StructuredContent landen (fruehere Fassung
        // listete dort ALLE gescannten Dateien, nicht nur critical/warning — blaehte die Antwort
        // bei einer grossen Solution auf mehrere zehntausend Zeichen auf).
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("hotspots")
            .Deserialize<List<HotspotEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Empty(entries!);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles()
    {
        var state = _fixture.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, "SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Gescannt: 5 .cs-Dateien", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterWithForwardSlashPath_MatchesFiles()
    {
        var state = _fixture.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, "src/SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Gescannt: 5 .cs-Dateien", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, "DoesNotExistAnywhere", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("im gruenen Bereich", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithPluralAggregateWarning()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        CompileErrorHeaderAssertions.AssertStartsWithCompileErrorHeader(text, expectedFileCount: 3);
    }

    [Fact]
    public async Task ExecuteAsync_SingleCompileErrorFixture_OutputStartsWithSingularAggregateWarning()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreateSingular());
        using var state = context.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        CompileErrorHeaderAssertions.AssertStartsWithCompileErrorHeader(text, expectedFileCount: 1);
    }
}
