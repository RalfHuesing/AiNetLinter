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

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

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
        Assert.Contains("Gescannt: 6 .cs-Dateien", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterWithForwardSlashPath_MatchesFiles()
    {
        var state = _fixture.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, "src/SymbolGraphMini", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Gescannt: 6 .cs-Dateien", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultScopeType_ExcludesTestProjects()
    {
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("Production", [("Service.cs", "namespace Probe; public sealed class Service { }")], VirtualProjectDirectory: "src/Production"),
            new ProjectSpec("Production.Tests", [("ServiceTests.cs", "namespace Probe.Tests; public sealed class ServiceTests { }")], VirtualProjectDirectory: "tests/Production.Tests")));
        using var state = context.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(
            new GetHotspotsRequest(
                state,
                ScopeFilter: null,
                MaxResults: 50,
                MinLinePercentage: 0,
                ScopeType: null,
                CancellationToken: CancellationToken.None));

        var payload = DeserializePayload(result);
        Assert.Single(payload.Hotspots);
        Assert.Contains("Service.cs", payload.Hotspots.Single().RelativePath, StringComparison.Ordinal);
        Assert.Equal("production", payload.ScopeType);
        Assert.Contains("Gescannt: 1 .cs-Dateien", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TestScopeType_IncludesOnlyTestProjects()
    {
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("Production", [("Service.cs", "namespace Probe; public sealed class Service { }")], VirtualProjectDirectory: "src/Production"),
            new ProjectSpec("Production.Tests", [("ServiceTests.cs", "namespace Probe.Tests; public sealed class ServiceTests { }")], VirtualProjectDirectory: "tests/Production.Tests")));
        using var state = context.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(
            new GetHotspotsRequest(
                state,
                ScopeFilter: null,
                MaxResults: 50,
                MinLinePercentage: 0,
                ScopeType: "tests",
                CancellationToken: CancellationToken.None));

        var payload = DeserializePayload(result);
        Assert.Single(payload.Hotspots);
        Assert.Contains("ServiceTests.cs", payload.Hotspots.Single().RelativePath, StringComparison.Ordinal);
        Assert.Equal("tests", payload.ScopeType);
        Assert.Contains("Gescannt: 1 .cs-Dateien", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScopeType_ReturnsRecoverableInvalidArgument()
    {
        var result = await GetHotspotsTool.ExecuteAsync(
            new GetHotspotsRequest(
                _fixture.CreateServer(),
                ScopeFilter: null,
                MaxResults: 50,
                MinLinePercentage: 80,
                ScopeType: "unknown",
                CancellationToken: CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        Assert.Contains("scopeType", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsAndMinLinePercentage_KeepDeterministicBoundedResults()
    {
        var state = _fixture.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(
            state,
            null,
            maxResults: 1,
            minLinePercentage: 0,
            CancellationToken.None);

        var payload = DeserializePayload(result);
        Assert.Equal(6, payload.TotalHotspots);
        Assert.Equal(1, payload.ShownHotspots);
        Assert.True(payload.Truncated);
        Assert.Single(payload.Hotspots);
        Assert.Equal(1, payload.MaxResults);
        Assert.Equal(0, payload.MinLinePercentage);
        Assert.Contains("Hotspots gesamt, 1 gezeigt", TextOf(result), StringComparison.Ordinal);
        Assert.Equal(
            payload.Hotspots
                .OrderByDescending(entry => entry.Lines)
                .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase),
            payload.Hotspots);
    }

    [Fact]
    public async Task ExecuteAsync_HotspotParameters_ClampToMeaningfulBounds()
    {
        var state = _fixture.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(
            state,
            null,
            maxResults: 0,
            minLinePercentage: -10,
            CancellationToken.None);

        var payload = DeserializePayload(result);
        Assert.Equal(GetHotspotsScanner.DefaultMaxResults, payload.MaxResults);
        Assert.Equal(GetHotspotsScanner.MinLinePercentage, payload.MinLinePercentage);
        Assert.Equal(payload.TotalHotspots, payload.ShownHotspots);
        Assert.False(payload.Truncated);
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

    private static HotspotsPayload DeserializePayload(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return JsonSerializer.Deserialize<HotspotsPayload>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default)!;
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
