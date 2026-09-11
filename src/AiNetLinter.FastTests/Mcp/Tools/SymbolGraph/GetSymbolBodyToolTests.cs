#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.DoesNotContain("id:", textContent.Text, System.StringComparison.Ordinal);
        Assert.True(result.StructuredContent.HasValue);
        var structured = result.StructuredContent!.Value;
        var entry = Assert.Single(structured.GetProperty("results").EnumerateArray());
        Assert.StartsWith("s:", entry.GetProperty("id").GetString(), System.StringComparison.Ordinal);
        Assert.NotEqual(stableId, entry.GetProperty("id").GetString());
        Assert.False(entry.GetProperty("isTruncated").GetBoolean());
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScalarSymbolIdentifier_ReturnsBodyForMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(SymbolIdentifiers: ["Greeter.Greet"], MaxBodyLines: 80),
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
        Assert.DoesNotContain("id: `s:", textContent.Text, System.StringComparison.Ordinal);
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
        Assert.Equal(2, textContent.Text.Split("### ", System.StringSplitOptions.None).Length - 1);
        Assert.Contains("---", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleNamedIdentifiers_EmitsBothRequestedWithoutResolvedId()
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
        Assert.DoesNotContain("id: `s:", textContent.Text, System.StringComparison.Ordinal);
        Assert.Equal(2, result.StructuredContent!.Value.GetProperty("results").GetArrayLength());
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
        Assert.DoesNotContain("id: `s:", textContent.Text, System.StringComparison.Ordinal);
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

    [Fact]
    public async Task ExecuteAsync_WithEndLine_CalculatesEffectiveMaxBodyLines()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var request = new GetSymbolBodyRequest(
            SymbolIdentifiers: [stableId!],
            StartLine: 1,
            EndLine: 2);

        Assert.Equal(2, request.EffectiveMaxBodyLines);

        var invertedRequest = new GetSymbolBodyRequest(
            SymbolIdentifiers: [stableId!],
            StartLine: 10,
            EndLine: 5);
        Assert.Equal(1, invertedRequest.EffectiveMaxBodyLines);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, request, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Zeilen: 1-2 von", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FileLineInsideMethodBody_ResolvesEnclosingMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            ["Caller.cs:9"],
            80,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Run", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DocCommentIdWithoutParameters_ResolvesMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            ["M:SymbolGraphMini.Greeter.Greet"],
            80,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greet", text, StringComparison.Ordinal);
        Assert.Contains("Hello", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DocCommentIdWithWrongParameters_ResolvesOrSuggestsCorrectMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            ["M:SymbolGraphMini.Greeter.Greet(System.Int32)"],
            80,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greet", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIdIsStructuredOnly_AndBodyRemainsComplete()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(
            state,
            ["Greeter.Greet"],
            80,
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("id:", text, System.StringComparison.Ordinal);
        Assert.Contains("Hello", text, System.StringComparison.Ordinal);

        var entry = result.StructuredContent!.Value.GetProperty("results")[0];
        Assert.StartsWith("s:", entry.GetProperty("id").GetString(), System.StringComparison.Ordinal);
        Assert.Equal("member", entry.GetProperty("handoffKind").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ResponseBudget_SelectsWholeSymbolBodyUnitsOrReturnsMinimumError()
    {
        var state = _fixture.CreateServer();
        var identifiers = new[] { "Greeter.Greet", "Caller.Run" };

        var constrained = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(identifiers, MaxResponseBytes: 512),
            CancellationToken.None);
        var expanded = await GetSymbolBodyTool.ExecuteAsync(
            state,
            new GetSymbolBodyRequest(identifiers, MaxResponseBytes: 4_096),
            CancellationToken.None);

        Assert.NotEqual(true, expanded.IsError);
        var expandedResults = expanded.StructuredContent!.Value.GetProperty("results").GetArrayLength();
        if (constrained.IsError == true)
        {
            Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", Assert.IsType<TextContentBlock>(Assert.Single(constrained.Content)).Text, StringComparison.Ordinal);
        }
        else
        {
            var constrainedText = Assert.IsType<TextContentBlock>(Assert.Single(constrained.Content)).Text;
            var constrainedStructured = constrained.StructuredContent!.Value.GetRawText();
            Assert.True(
                Encoding.UTF8.GetByteCount(constrainedText) + Encoding.UTF8.GetByteCount(constrainedStructured) <= 512,
                "Symbol-Body-Text und StructuredContent muessen gemeinsam ins Budget passen.");
            Assert.True(constrained.StructuredContent!.Value.GetProperty("results").GetArrayLength() <= expandedResults);
        }

        Assert.Equal(identifiers.Length, expanded.StructuredContent!.Value.GetProperty("requestedCount").GetInt32());
    }

    [Fact]
    public void ApplyFinalResponseBudget_NavigationOverflowDropsWholeUnitsBeforeMinimumError()
    {
        var entries = Enumerable.Range(1, 3).Select(index => new SymbolBodyEntry(
            $"M:Demo.Work{index}", $"M:Demo.Work{index}", "member", "Demo.cs", index,
            new string('x', 220), "available", "source", false)).ToList();
        var root = JsonSerializer.SerializeToNode(new SymbolBodyBatchDto(entries, entries.Count), McpJsonOptions.Default)!.AsObject();
        root["navigation"] = new JsonObject { ["status"] = new JsonObject { ["operation"] = new string('n', 180) } };
        var original = McpToolResults.Text("original", root);

        var projected = GetSymbolBodyTool.ApplyFinalResponseBudget(original, 1_024);
        if (projected.StructuredContent!.Value.TryGetProperty("code", out var code))
        {
            Assert.Equal("RESPONSE_BUDGET_TOO_SMALL", code.GetString());
            return;
        }

        Assert.True(projected.StructuredContent!.Value.GetProperty("results").GetArrayLength() < entries.Count);
        Assert.True(Encoding.UTF8.GetByteCount(Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text)
            + Encoding.UTF8.GetByteCount(projected.StructuredContent!.Value.GetRawText()) <= 1_024);
    }
}

