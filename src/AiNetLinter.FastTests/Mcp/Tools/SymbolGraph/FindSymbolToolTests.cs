#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class FindSymbolToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await FindSymbolTool.ExecuteAsync(state, ["irrelevant"], null, 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    public static IEnumerable<object?[]> EmptyCases =>
    [
        [null],
        [System.Array.Empty<string>()],
        [new[] { "", "   " }]
    ];

    [Theory]
    [MemberData(nameof(EmptyCases))]
    public async Task ExecuteAsync_EmptyNamePatterns_ReturnsRecoverableInvalidArgument(string[]? patterns)
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePatterns: patterns, kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Pflichtparameter 'namePatterns' fehlt oder ist leer.", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("namePatterns: [\"Greeter\"]", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxPatternsCap_ReturnsRecoverableInvalidArgument()
    {
        using var fixture = new McpInMemoryTestContext();
        var elevenPatterns = Enumerable.Range(1, 11).Select(i => $"Pattern{i}").ToArray();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePatterns: elevenPatterns, kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Maximal 10 namePatterns pro Call erlaubt", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("angefordert: 11", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TenPatterns_IsAllowed()
    {
        using var fixture = new McpInMemoryTestContext();
        var tenPatterns = Enumerable.Range(1, 10).Select(i => $"Pattern{i}").ToArray();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePatterns: tenPatterns, kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        Assert.Equal(10, batch.Results.Count);
    }

    [Fact]
    public async Task ExecuteAsync_KnownSymbol_StructuredContentDeserializesToFindSymbolBatchDto()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["Greeter"], kind: "class", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        var singleResult = Assert.Single(batch.Results);
        Assert.Equal("Greeter", singleResult.NamePattern);
        Assert.Contains(singleResult.Matches, entry => entry.FilePath.Contains("Greeter.cs", System.StringComparison.Ordinal) && entry.Kind == "Klasse");
    }

    [Fact]
    public async Task ExecuteAsync_MultiplePatterns_ReturnsSectionsWithDividers()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["Greeter", "Caller"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Symbol-Suche: `Greeter`", textContent.Text);
        Assert.Contains("Symbol-Suche: `Caller`", textContent.Text);
        Assert.Contains("---", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Results.Count);
        Assert.Equal("Greeter", batch.Results[0].NamePattern);
        Assert.Equal("Caller", batch.Results[1].NamePattern);
    }

    [Fact]
    public async Task ExecuteAsync_MultiplePatterns_OneMatchOneMiss_ContinuesAndIncludesMissHint()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["Greeter", "NonExistentXyz"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Symbol-Suche: `Greeter`", textContent.Text);
        Assert.Contains("Greeter.cs", textContent.Text);
        Assert.Contains("Symbol-Suche: `NonExistentXyz`", textContent.Text);
        Assert.Contains("Keine Treffer fuer 'NonExistentXyz'", textContent.Text);

        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Results.Count);
        Assert.NotEmpty(batch.Results[0].Matches);
        Assert.Empty(batch.Results[1].Matches);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicatePatterns_DeduplicatesOrdinal()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["Greeter", "Greeter"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Single(textContent.Text.Split("Symbol-Suche: `Greeter`").Skip(1));

        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        Assert.Single(batch.Results);
    }

    [Fact]
    public async Task ExecuteAsync_CaseDifference_KeepsBothEntries()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["greeter", "Greeter"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var batch = JsonSerializer.Deserialize<FindSymbolBatchDto>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Results.Count);
        Assert.Equal("greeter", batch.Results[0].NamePattern);
        Assert.Equal("Greeter", batch.Results[1].NamePattern);
    }

    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
        Assert.Contains(":", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_GermanKindKlasse_BehavesLikeEnglishClass()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: "Klasse", maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownKind_ReturnsRecoverableInvalidArgument()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePatterns: ["Greeter"], kind: "Enum", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Enum", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindMatchesAndFormat_CaseInsensitive_MatchesRegardlessOfCase()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter", result);
    }

    [Fact]
    public async Task ExecuteAsync_MultiplePatterns_TruncatesIndividuallyPerPattern()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["Greet", "Call"], kind: null, maxResults: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt, 1 gezeigt", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["ValidClassA"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.Contains("ValidClassA", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultiplePatterns_CompileErrorFixture_ShowsWarningExactlyOnce()
    {
        using var fixture = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), ["ValidClassA", "ValidClassB"], kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, System.StringComparison.Ordinal);
        // Hinweis darf genau 1 Mal in der Gesamtausgabe vorkommen
        var count = text.Split("Hinweis:").Length - 1;
        Assert.Equal(1, count);
    }
}
