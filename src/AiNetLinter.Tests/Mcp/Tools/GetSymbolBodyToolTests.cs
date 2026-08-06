using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class GetSymbolBodyToolTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetSymbolBodyToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);
        Assert.NotNull(stableId);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, stableId!, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("id:", textContent.Text, System.StringComparison.Ordinal);
        // Q5 Sufficiency-Hinweis: vollstaendiger (nicht gekappter) Body ist final.
        Assert.Contains("vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidStableId_TruncatesAtMaxBodyLines_AppendsEllipsis()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(symbol!);

        var result = await GetSymbolBodyTool.ExecuteAsync(state, stableId!, 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("truncated", textContent.Text, System.StringComparison.OrdinalIgnoreCase);
        // Q5: ein per maxBodyLines gekappter Body bekommt NICHT den "vollstaendig"-Hinweis.
        Assert.DoesNotContain("vollstaendig", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStableId_FallsBackToFileLineCol()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var identifier = $"{_fixture.Workspace.GreeterPath}:5:19";
        var result = await GetSymbolBodyTool.ExecuteAsync(state, identifier, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStableId_AndFileLineColNotFound_ReturnsRecoverableSymbolNotFound()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetSymbolBodyTool.ExecuteAsync(state, "DoesNotExistXyz", 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var identifier = $"{_fixture.Workspace.GreeterPath}:7:28";
        var result = await GetSymbolBodyTool.ExecuteAsync(state, identifier, 80, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("id: `P:SymbolGraphMini.Greeter.Prefix`", textContent.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("get_Prefix", textContent.Text, System.StringComparison.Ordinal);
    }
}
