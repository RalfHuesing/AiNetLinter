using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class CallGraphTraversalTests
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

    // --- BuildTreeAsync (get_call_tree) ---
    // Fixture: Greeter.Greet wird von drei Methoden in Caller.cs aufgerufen — Run (1x),
    // RunTwice (2x), RunThrice (3x) — niemand ruft Run/RunTwice/RunThrice wiederum auf.
    // Das ergibt genau 3 Kinder auf Ebene 1 und eine leere Ebene 2 (echter Baum-Abschluss).

    [Fact]
    public async Task BuildTreeAsync_Depth1_RootHasThreeDistinctCallerChildren()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(3, root.Children.Count);
        Assert.Contains(root.Children, c => c.Name == "Caller.Run");
        Assert.Contains(root.Children, c => c.Name == "Caller.RunTwice");
        Assert.Contains(root.Children, c => c.Name == "Caller.RunThrice");
        // Depth=1: keine weitere Rekursion, Kinder sind Blaetter.
        Assert.All(root.Children, c => Assert.Empty(c.Children));
    }

    [Fact]
    public async Task BuildTreeAsync_MultipleCallSitesInSameCaller_GroupedIntoOneChildWithCountMarker()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

        var runThrice = root.Children.Single(c => c.Name == "Caller.RunThrice");
        // RunThrice ruft Greet dreimal auf — ein Knoten, DisplayLine traegt den "+N weitere"-Marker.
        Assert.Contains("+2 weitere Aufrufe", runThrice.DisplayLine, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildTreeAsync_Depth2_NoFurtherCallersOfCaller_ChildrenStayLeaves()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 2, topN: 10, CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(3, root.Children.Count);
        // Niemand ruft Caller.Run/RunTwice/RunThrice auf — Ebene 2 bleibt leer, echter Baum-Abschluss.
        Assert.All(root.Children, c => Assert.Empty(c.Children));
    }

    [Fact]
    public async Task BuildTreeAsync_DepthAboveCap_ClampsToMaxCallTreeDepth()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 99, topN: 10, CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(3, root.Children.Count);
    }

    [Fact]
    public async Task BuildTreeAsync_TopNBelowChildCount_KeepsAllChildrenInTreeForRendererCap()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 2, topN: 2, CancellationToken.None);

        // topN begrenzt nur die weitere Rekursion, nicht die Sichtbarkeit im Baum — der
        // MetricsTreeRenderer wendet seine eigene Top-N-Kappung ("... und N weitere") an.
        Assert.Equal(3, root.Children.Count);
    }

    [Fact]
    public async Task BuildTreeAsync_RootDisplayLineContainsGreeterFile()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTraversal.BuildTreeAsync(
            _fixture.Catalog.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

        Assert.Equal("Greeter.Greet", root.Name);
        Assert.Contains("Greeter.cs", root.DisplayLine, System.StringComparison.Ordinal);
    }
}
