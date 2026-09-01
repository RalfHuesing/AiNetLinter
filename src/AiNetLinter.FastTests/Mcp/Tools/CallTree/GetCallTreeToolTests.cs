#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

[Trait("Category", "Component")]
public sealed class GetCallTreeToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetCallTreeToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("irrelevant", 2, null, 10), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("DoesNotExistXyz", 2, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbol()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Run", 2, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AsciiFormatDefault_ReturnsTreeWithCallerNames()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.Run", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        // ASCII-Baum: Kindzeilen tragen den Renderer-eigenen Praefix.
        Assert.Contains("├──", textContent.Text, StringComparison.Ordinal);
        // Sufficiency-Hinweis fuer nicht-trunkierte Ergebnisse.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCallTree_ReturnsStructuredSuccessPayload()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<CallTreePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("incoming", payload!.Direction);
        Assert.Equal(1, payload.RequestedDepth);
        Assert.Equal(10, payload.TopN);
        Assert.False(payload.Truncated);
        Assert.False(payload.TopNTruncated);
        Assert.NotEmpty(payload.Root.Children);
        Assert.Contains(payload.Root.Children, child => child.Name.Contains("Caller", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MermaidFormat_ReturnsFlowchartBlock()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, "mermaid", 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("flowchart TD", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("-->", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Caller.Run", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TopNBelowCallerCount_AppendsRemainingCountLine()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // Greeter.Greet hat 3 distinkte Aufrufer (Run/RunTwice/RunThrice) — topN=1 zeigt 1,
        // der Renderer haengt die "... und N weitere"-Zeile an.
        Assert.Contains("... und 2 weitere", textContent.Text, StringComparison.Ordinal);
        // Regression: eine reine Renderer-Top-N-Kappung (kein 250-Knoten-Hardcap) muss trotzdem
        // als trunkiert erkannt werden — sonst behauptet der Sufficiency-Hinweis faelschlich
        // Vollstaendigkeit, obwohl sichtbar Kinder fehlen.
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("trunkiert", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("topN erhoehen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsAndStillReturnsResult()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 99, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("ValidClassA.DoWork", 1, null, 10), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OutgoingDirection_ReturnsCalleeNames()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("SymbolGraphMini.Caller.Run", 1, null, 10, "outgoing"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.Greet", text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[outgoing]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDirection_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            state, new GetCallTreeInput("Greeter.Greet", 1, null, 10, "sideways"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("INVALID_ARGUMENT", text, StringComparison.Ordinal);
        Assert.Contains("direction", text, StringComparison.Ordinal);
    }
}
