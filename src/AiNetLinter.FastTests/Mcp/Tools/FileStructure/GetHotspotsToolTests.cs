#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
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
    public async Task ExecuteAsync_SmallMaxLineCount_ReportsCriticalCategoryInContent()
    {
        var state = _fixture.CreateServer(1);

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Kritische Dateien", text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
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
        Assert.Contains("Keine Datei erreicht", textContent.Text, StringComparison.Ordinal);
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

        var text = TextOf(result);
        Assert.Contains("Service.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceTests.cs", text, StringComparison.Ordinal);
        Assert.Contains("Gescannt: 1 .cs-Dateien", text, StringComparison.Ordinal);
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

        var text = TextOf(result);
        Assert.Contains("ServiceTests.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Service.cs", text, StringComparison.Ordinal);
        Assert.Contains("Gescannt: 1 .cs-Dateien", text, StringComparison.Ordinal);
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

        var text = TextOf(result);
        Assert.Contains("Hotspots gesamt, 1 gezeigt", text, StringComparison.Ordinal);
        Assert.Contains("maxResults", text, StringComparison.Ordinal);
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

        var text = TextOf(result);
        Assert.Contains("MaxLineCount: 1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("maxResults erhöhen", text, StringComparison.Ordinal);
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
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetHotspotsTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
