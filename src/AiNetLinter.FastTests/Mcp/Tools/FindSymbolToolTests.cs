#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class FindSymbolToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyNamePattern_ReturnsRecoverableInvalidArgument()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePattern: "", kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Pattern angeben", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_KnownSymbol_StructuredContentDeserializesToSymbolLocationEntries()
    {
        using var fixture = new McpInMemoryTestContext();
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), "Greeter", kind: "class", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("matches")
            .Deserialize<List<SymbolLocationEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Contains(entries!, entry => entry.FilePath.Contains("Greeter.cs", System.StringComparison.Ordinal) && entry.Kind == "Klasse");
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
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), namePattern: "Greeter", kind: "Enum", maxResults: 50, CancellationToken.None);

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
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        var result = await FindSymbolTool.ExecuteAsync(fixture.CreateServer(), "ValidClassA", kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.Contains("ValidClassA", text, System.StringComparison.Ordinal);
    }
}
