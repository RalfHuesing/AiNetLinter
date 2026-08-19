#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
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

        var result = await GetSymbolBodyTool.ExecuteAsync(state, "irrelevant", 80, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
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

        var result = await GetSymbolBodyTool.ExecuteAsync(state, stableId!, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("id:", textContent.Text, System.StringComparison.Ordinal);
        // Sufficiency-Hinweis: vollstaendiger (nicht gekappter) Body ist final.
        Assert.Contains("vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidStableId_TruncatesAtMaxBodyLines_AppendsEllipsis()
    {
        var state = _fixture.CreateServer();

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, stableId!, 1, CancellationToken.None);

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
        var result = await GetSymbolBodyTool.ExecuteAsync(state, identifier, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStableId_AndFileLineColNotFound_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetSymbolBodyTool.ExecuteAsync(state, "DoesNotExistXyz", 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId()
    {
        var state = _fixture.CreateServer();

        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:7:28";
        var result = await GetSymbolBodyTool.ExecuteAsync(state, identifier, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("id: `P:SymbolGraphMini.Greeter.Prefix`", textContent.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("get_Prefix", textContent.Text, System.StringComparison.Ordinal);
    }
}
