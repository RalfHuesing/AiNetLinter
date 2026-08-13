#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

/// <summary>
/// Tool-Layer-Tests fuer <see cref="PatternDetectTool"/>: Validierung (unbekannte pattern-IDs,
/// leerer Filter = alle Patterns), IsError-Policy (SOLUTION_NOT_LOADED, recoverable
/// INVALID_ARGUMENT) und StructuredContent-Form. Pattern 1:1 von <c>GetViolationsToolTests</c>
/// uebernommen.
/// </summary>
[Trait("Category", "Component")]
public sealed class PatternDetectToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public PatternDetectToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await PatternDetectTool.ExecuteAsync(state, null, null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NullPatternsFilter_ReturnsAllSixPatternsInStructuredContent()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, null, null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.Equal(6, json["patterns"]!.AsArray().Count);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPatternsArray_MeansAllPatterns()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, [], null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.Equal(6, json["patterns"]!.AsArray().Count);
    }

    [Fact]
    public async Task ExecuteAsync_SinglePatternFilter_ReturnsOnlyThatPatternInStructuredContent()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, ["async-void"], null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        var patterns = json["patterns"]!.AsArray();
        Assert.Single(patterns);
        Assert.Equal("async-void", (string)patterns[0]!["id"]!);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPatternId_ReturnsRecoverableInvalidArgumentNotIsError()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, ["definitely-not-a-pattern"], null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        // Recoverable (IsErrorPolicy.md): ein Tippfehler in der pattern-ID ist ein erwartbarer
        // Nutzerfehler mit Handlungsanleitung im Text, kein Tool-Ausfall.
        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("definitely-not-a-pattern", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("god-class", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessageWithoutStructuredContent()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, null, "DoesNotExistAnywhere", PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Null(result.StructuredContent);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReportTextMentionsPatternDetectHeaderAndSufficiencyHint()
    {
        var state = _fixture.CreateServer();

        var result = await PatternDetectTool.ExecuteAsync(state, null, null, PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken.None);

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Pattern-Detect:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }
}
