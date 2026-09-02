using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class SearchPatternToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public SearchPatternToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "anything", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "Greeter", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RegexPattern_FindsExpectedHitsInFixture()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "^public\\s+(class|interface|record)",
            isRegex: true,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("public class", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PlainTextTruncatesAtMaxResults_AppendsMetaLine()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "public", isRegex: false, maxResults: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("[", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);

        var lines = textContent.Text.Split('\n');
        var metaIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("[", System.StringComparison.Ordinal) && lines[i].Contains("Treffer gesamt"))
            {
                metaIndex = i;
                break;
            }
        }
        Assert.True(metaIndex >= 2, $"Erwartete mind. 2 Trefferzeilen vor der Meta-Zeile, Output:\n{textContent.Text}");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_ReturnsZeroHitsMessage()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "thisStringDoesNotExistAnywhere_zzz_999",
            isRegex: false,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("0 Treffer", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromHits()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var projectDir = Path.GetDirectoryName(fixture.GreeterPath)!;
        var generatedDir = Path.Combine(projectDir, "obj", "Debug");
        Directory.CreateDirectory(generatedDir);
        File.WriteAllText(Path.Combine(generatedDir, "Generated.cs"), "PATTERN_ANCHOR_999 content");

        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "PATTERN_ANCHOR_999",
            isRegex: false,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("Generated.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WorktreeSubdirectory_ExcludedFromHits()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var projectDir = Path.GetDirectoryName(fixture.GreeterPath)!;
        var worktreeDir = Path.Combine(projectDir, "worktrees", "agent-x", "src");
        Directory.CreateDirectory(worktreeDir);
        File.WriteAllText(Path.Combine(worktreeDir, "Duplicate.cs"), "PATTERN_ANCHOR_WORKTREE_777 content");

        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            pattern: "PATTERN_ANCHOR_WORKTREE_777",
            isRegex: false,
            maxResults: 50,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("0 Treffer", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Duplicate.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRegex_ReturnsRecoverableInvalidArgument()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "(unclosed", isRegex: true, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPattern_ReturnsRecoverableInvalidArgument()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern angeben", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("gitRef", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "ValidClass", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredContent_PreservesLegacyTextAndReturnsObjectPayload()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 0, null, null, null),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Array, result.StructuredContent.Value.GetProperty("matches").ValueKind);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.GetProperty("completeness").ValueKind);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.GetProperty("scope").ValueKind);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent.Value.GetProperty("snapshot").ValueKind);
        Assert.DoesNotContain(
            result.StructuredContent.Value.GetProperty("matches").EnumerateArray(),
            match => match.TryGetProperty("semantic", out _));
    }

    [Fact]
    public async Task ExecuteAsync_EnrichCSharp_ReturnsSemanticObjectAndKeepsLegacyText()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 0, null, null, null, true),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        var matches = result.StructuredContent!.Value.GetProperty("matches").EnumerateArray().ToArray();
        var declaration = Assert.Single(matches.Where(match =>
            match.GetProperty("semantic").GetProperty("kind").GetString() == "declaration"));
        Assert.Equal("resolved", declaration.GetProperty("semantic").GetProperty("resolution").GetString());
        Assert.Equal("T:SymbolGraphMini.Greeter", declaration.GetProperty("semantic").GetProperty("symbolId").GetString());
        Assert.Contains(matches, match =>
            match.GetProperty("semantic").GetProperty("kind").GetString() == "symbol_reference");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMatchesAndContext_ReturnRangesAndBoundedContext()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("search-anchor", false, 50, 0, 1, 0, null, null, null),
            CancellationToken.None);

        var matches = result.StructuredContent!.Value.GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal(2, matches.Length);
        var fixtureMatch = Assert.Single(matches.Where(match =>
            match.GetProperty("filePath").GetString()!.EndsWith(".md", StringComparison.Ordinal)));
        Assert.Equal(2, fixtureMatch.GetProperty("matchRanges").GetArrayLength());
        Assert.True(fixtureMatch.GetProperty("contextBefore").GetArrayLength() <= 1);
        Assert.True(fixtureMatch.GetProperty("contextAfter").GetArrayLength() <= 1);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsAndMaxFiles_ReportVisibleAndTotalCounts()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("search-anchor", false, 50, 1, 0, 0, null, null, null),
            CancellationToken.None);

        var completeness = result.StructuredContent!.Value.GetProperty("completeness");
        Assert.Equal(2, completeness.GetProperty("matchedFileCount").GetInt32());
        Assert.Equal(1, completeness.GetProperty("shownMatchedFileCount").GetInt32());
        Assert.Contains(
            "maxFiles",
            completeness.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_SetsCompletenessReason()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("search-anchor", false, 50, 0, 1, 200, null, null, null),
            CancellationToken.None);

        var reasons = result.StructuredContent!.Value.GetProperty("completeness")
            .GetProperty("truncatedBy")
            .EnumerateArray()
            .Select(item => item.GetString());
        Assert.Contains("maxResponseBytes", reasons);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeAndFilters_RespectGenericRelativePaths()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments(
                "search-anchor",
                false,
                50,
                0,
                0,
                0,
                "src/SymbolGraphMini",
                new[] { "**/*.json" },
                new[] { "**/*.md" }),
            CancellationToken.None);

        var matches = result.StructuredContent!.Value.GetProperty("matches").EnumerateArray().ToArray();
        var match = Assert.Single(matches);
        Assert.EndsWith("search-fixture.json", match.GetProperty("filePath").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(matches, item => item.GetProperty("filePath").GetString()!.EndsWith(".md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidBudgets_ReturnRecoverableInvalidArgument()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 50, -1, 0, 0, null, null, null),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(
            "INVALID_ARGUMENT",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultCall_RetainsLegacyOutputSemantics()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await SearchPatternTool.ExecuteAsync(
            state, pattern: "does-not-exist", isRegex: false, maxResults: 50, CancellationToken.None);

        Assert.Contains(
            "0 Treffer",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
        Assert.Empty(result.StructuredContent!.Value.GetProperty("matches").EnumerateArray());
    }
}
