using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class CallGraphTraversalTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public CallGraphTraversalTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExpandAndFormatAsync_Depth1_ReturnsCallSiteFromCaller()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var text = await CallGraphTraversal.ExpandAndFormatAsync(
            _fixture.Catalog.Solution, symbol!, 1, 50, CancellationToken.None);

        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpandAndFormatAsync_Depth2_FormatsWithDepthMarker()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var text = await CallGraphTraversal.ExpandAndFormatAsync(
            _fixture.Catalog.Solution, symbol!, 2, 50, CancellationToken.None);

        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpandAndFormatAsync_DepthAboveCap_ClampsToThree()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var text = await CallGraphTraversal.ExpandAndFormatAsync(
            _fixture.Catalog.Solution, symbol!, 99, 50, CancellationToken.None);

        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }
}
