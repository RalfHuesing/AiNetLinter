#nullable enable

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
public sealed class GetSymbolBodyToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetSymbolBodyToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetSymbolBodyTool.ExecuteAsync(state, ["irrelevant"], 80, CancellationToken.None);

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
    public async Task ExecuteAsync_EmptySymbolIdentifiers_ReturnsRecoverableInvalidArgument(string[]? symbolIdentifiers)
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(state, symbolIdentifiers, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.", textContent.Text);
        Assert.Contains("symbolIdentifiers: [\"M:Klasse.Methode\"]", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidStableId_ReturnsBodyForMethod()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);
        Assert.NotNull(stableId);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, [stableId!], 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("id:", textContent.Text, System.StringComparison.Ordinal);
        Assert.True(result.StructuredContent.HasValue);
        var structured = result.StructuredContent!.Value;
        var entry = Assert.Single(structured.GetProperty("results").EnumerateArray());
        Assert.Equal(stableId, entry.GetProperty("id").GetString());
        Assert.False(entry.GetProperty("isTruncated").GetBoolean());
        // Sufficiency-Hinweis: vollstaendiger (nicht gekappter) Body ist final.
        Assert.Contains("vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScalarSymbolIdentifierAlias_ReturnsBodyForMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(SymbolIdentifier: "Greeter.Greet", MaxBodyLines: 80),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(
            "Greet",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidStableId_TruncatesAtMaxBodyLines_AppendsEllipsis()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, [stableId!], 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("truncated", textContent.Text, System.StringComparison.OrdinalIgnoreCase);
        // Ein per maxBodyLines gekappter Body bekommt NICHT den "vollstaendig"-Hinweis.
        Assert.DoesNotContain("vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStableId_FallsBackToFileLineCol()
    {
        var state = _fixture.CreateServer();

        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:5:19";
        var result = await GetSymbolBodyTool.ExecuteAsync(state, [identifier], 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStableId_AndFileLineColNotFound_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(state, ["DoesNotExistXyz"], 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId()
    {
        var state = _fixture.CreateServer();

        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:7:28";
        var result = await GetSymbolBodyTool.ExecuteAsync(state, [identifier], 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("id: `P:SymbolGraphMini.Greeter.Prefix`", textContent.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("get_Prefix", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleStableIds_ReturnsAllBodiesInSingleTurn()
    {
        var state = _fixture.CreateServer();

        var (symbol1, _) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var (symbol2, _) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "Greeter.Prefix", CancellationToken.None);
        Assert.NotNull(symbol1);
        Assert.NotNull(symbol2);

        var stableId1 = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol1!);
        var stableId2 = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol2!);

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            symbolIdentifiers: [stableId1!, stableId2!],
            maxBodyLines: 80,
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Prefix", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains($"id: `{stableId1}`", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains($"id: `{stableId2}`", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("---", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleNamedIdentifiers_EmitsBothRequestedAndResolvedId()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            symbolIdentifiers: ["Greeter.Greet", "Greeter.Prefix"],
            maxBodyLines: 80,
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("angefordert: `Greeter.Greet`", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("angefordert: `Greeter.Prefix`", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("id: `M:SymbolGraphMini.Greeter.Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("id: `P:SymbolGraphMini.Greeter.Prefix`", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleIdentifiers_WithOneNotFound_ContinuesAndIncludesWarning()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            symbolIdentifiers: [stableId!, "DoesNotExistXyz"],
            maxBodyLines: 80,
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains($"id: `{stableId}`", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("DoesNotExistXyz", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("nicht aufgeloest", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithStartLineAndMaxBodyLines_ReturnsWindowedLines()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(SymbolIdentifiers: [stableId!], MaxBodyLines: 1, StartLine: 1),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Zeilen: 1-1 von", textContent.Text, System.StringComparison.Ordinal);
        Assert.True(result.StructuredContent.HasValue);
        var structured = result.StructuredContent!.Value;
        var entry = Assert.Single(structured.GetProperty("results").EnumerateArray());
        Assert.Equal(1, entry.GetProperty("displayedStartLine").GetInt32());
        Assert.Equal(1, entry.GetProperty("displayedEndLine").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_StartLineExceedsTotalLines_ReturnsOutOfBoundsMessage()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(SymbolIdentifiers: [stableId!], MaxBodyLines: 10, StartLine: 9999),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("liegt ausserhalb der Methode", textContent.Text, System.StringComparison.Ordinal);
    }
}

