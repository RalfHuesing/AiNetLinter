#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

[Trait("Category", "Component")]
public sealed class CallGraphTraversalTests
{
    private readonly McpInMemoryTestContext _fixture;

    public CallGraphTraversalTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExpandAsync_Depth1_FormatsCallSiteFromCaller()
    {
        // Bestands-Review: Ebene 1 war vom Enqueue-Defekt nie betroffen — die Eintraege kommen
        // direkt aus FindReferencesAsync des Startknotens. Test bleibt unverändert korrekt.
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var result = await CallGraphTraversal.ExpandAsync(
            _fixture.Solution, symbol!, 1, 50, CancellationToken.None);
        var text = TransitiveCallGraphFormatter.Format(result);

        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpandAsync_Depth2_FormatsWithDepthMarker()
    {
        // Bestands-Review mit Staerkung: die alte Assertion pruefte nur "Caller.cs" im Text und
        // bewies weder das Alt- noch das Neuverhalten. Auf dieser Fixture ruft niemand
        // Run/RunTwice/RunThrice — korrektes depth=2 endet deshalb nach Ebene 1 (echter
        // Kettenabschluss, keine erfundenen Tiefer-Eintraege). Die positive Kettenabdeckung
        // liegt bei ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels.
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var result = await CallGraphTraversal.ExpandAsync(
            _fixture.Solution, symbol!, 2, 50, CancellationToken.None);
        var text = TransitiveCallGraphFormatter.Format(result);

        Assert.NotEmpty(result.CallSites);
        Assert.All(result.CallSites, entry => Assert.Equal(1, entry.Depth));
        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels()
    {
        // Echte mehrstufige Aufruferkette A <- B <- C: depth=2 muss Ebene 1 (Aufruf in B) und
        // Ebene 2 (Aufruf in C) mit korrekter Herkunft liefern statt nach Ebene 1 abzubrechen.
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec("ChainProbe", [
            ("Chain.cs", """
                namespace ChainProbe;

                public class Runner
                {
                    public void MethodA() { }
                    public void MethodB() { MethodA(); }
                    public void MethodC() { MethodB(); }
                }
                """)
        ]));
        var (symbolA, _) = await FindReferencesTool.ResolveSymbolAsync(
            scenario.Solution, "ChainProbe.Runner.MethodA", CancellationToken.None);
        var (symbolB, _) = await FindReferencesTool.ResolveSymbolAsync(
            scenario.Solution, "ChainProbe.Runner.MethodB", CancellationToken.None);
        Assert.NotNull(symbolA);
        Assert.NotNull(symbolB);

        var result = await CallGraphTraversal.ExpandAsync(
            scenario.Solution, symbolA!, 2, 50, CancellationToken.None);

        var level1 = result.CallSites.Single(entry => entry.Depth == 1);
        Assert.Equal("Runner.MethodA", level1.SymbolName);
        Assert.Equal(DocumentationCommentId.CreateDeclarationId(symbolA!), level1.ReachedFromSymbolId);
        var level2 = result.CallSites.Single(entry => entry.Depth == 2);
        Assert.Equal("Runner.MethodB", level2.SymbolName);
        Assert.Equal(DocumentationCommentId.CreateDeclarationId(symbolB!), level2.ReachedFromSymbolId);
    }

    [Fact]
    public async Task ExpandAsync_DepthAboveCap_ClampsToThree()
    {
        // Bestands-Review: Clamp-Mechanik (requestedDepth=99 -> effectiveDepth=3) ist vom
        // Enqueue-Fix unberuehrt. Test bleibt unverändert korrekt.
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var result = await CallGraphTraversal.ExpandAsync(
            _fixture.Solution, symbol!, 99, 50, CancellationToken.None);
        var text = TransitiveCallGraphFormatter.Format(result);

        Assert.Contains("Caller.cs", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpandAsync_NodeLimit_ReportsNodeTruncationSeparately()
    {
        // Bestands-Review: Der Knoten-Cap bricht vor dem Besuch enqueuer Kinder ab — Mechanik
        // vom Enqueue-Fix unberuehrt, nach der Umstellung erneut verifiziert (weiterhin gruen).
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            fixture.Solution, "Contracts.IProcessor.Execute", CancellationToken.None);
        Assert.NotNull(symbol);

        var result = await CallGraphTraversal.ExpandAsync(
            new ReferenceTraversalRequest(
                fixture.Solution, symbol!, 3, 50, CancellationToken.None, 1));

        Assert.True(result.Completeness.TruncatedByNodeLimit);
        Assert.False(result.Completeness.TruncatedByMaxResults);
        Assert.Equal(1, result.Completeness.VisitedNodeCount);
    }

    // --- BuildTreeAsync (get_call_tree) ---
    // Fixture: Greeter.Greet wird von drei Methoden in Caller.cs aufgerufen — Run (1x),
    // RunTwice (2x), RunThrice (3x) — niemand ruft Run/RunTwice/RunThrice wiederum auf.
    // Das ergibt genau 3 Kinder auf Ebene 1 und eine leere Ebene 2 (echter Baum-Abschluss).

    [Fact]
    public async Task BuildTreeAsync_Depth1_RootHasThreeDistinctCallerChildren()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

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
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

        var runThrice = root.Children.Single(c => c.Name == "Caller.RunThrice");
        // RunThrice ruft Greet dreimal auf — ein Knoten, DisplayLine traegt den "+N weitere"-Marker.
        Assert.Contains("+2 weitere Aufrufe", runThrice.DisplayLine, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildTreeAsync_Depth2_NoFurtherCallersOfCaller_ChildrenStayLeaves()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 2, topN: 10, CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(3, root.Children.Count);
        // Niemand ruft Caller.Run/RunTwice/RunThrice auf — Ebene 2 bleibt leer, echter Baum-Abschluss.
        Assert.All(root.Children, c => Assert.Empty(c.Children));
    }

    [Fact]
    public async Task BuildTreeAsync_DepthAboveCap_ClampsToMaxCallTreeDepth()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 99, topN: 10, CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(3, root.Children.Count);
    }

    [Fact]
    public async Task BuildTreeAsync_TopNBelowChildCount_KeepsAllChildrenInTreeForRendererCap()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 2, topN: 2, CancellationToken.None);

        // topN begrenzt nur die weitere Rekursion, nicht die Sichtbarkeit im Baum — der
        // MetricsTreeRenderer wendet seine eigene Top-N-Kappung ("... und N weitere") an.
        Assert.Equal(3, root.Children.Count);
    }

    [Fact]
    public async Task BuildTreeAsync_RootDisplayLineContainsGreeterFile()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, _) = await CallGraphTreeBuilder.BuildTreeAsync(
            _fixture.Solution, symbol!, requestedDepth: 1, topN: 10, CancellationToken.None);

        Assert.Equal("Greeter.Greet", root.Name);
        Assert.Contains("Greeter.cs", root.DisplayLine, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildTreeAsync_Outgoing_ReturnsInvokedMethodsAndCreatedTypes()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "SymbolGraphMini.Caller.Run", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            new CallTreeBuildRequest(_fixture.Solution, symbol!, 1, 10, CallTreeDirection.Outgoing),
            CancellationToken.None);

        Assert.False(truncated);
        Assert.Contains(root.Children, child => child.Name == "Greeter");
        Assert.Contains(root.Children, child => child.Name == "Greeter.Greet");
    }

    [Fact]
    public async Task BuildTreeAsync_Both_LabelsChildrenWithTheirDirection()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "SymbolGraphMini.Caller.Run", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            new CallTreeBuildRequest(_fixture.Solution, symbol!, 1, 10, CallTreeDirection.Both),
            CancellationToken.None);

        Assert.False(truncated);
        Assert.Contains(root.Children, child => child.Name == "[outgoing] Greeter");
        Assert.Contains(root.Children, child => child.Name == "[outgoing] Greeter.Greet");
    }

    [Fact]
    public async Task BuildTreeAsync_Both_TopNShowsBothDirectionsBeforeOverflow()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec("Fairness", [
            ("Calls.cs", """
                namespace Fairness;
                public class Target
                {
                    public void Run() { new Helper(); }
                }
                public class Helper
                {
                }
                public class Caller
                {
                    public void Invoke() { new Target().Run(); }
                }
                """)
        ]));
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            scenario.Solution, "Fairness.Target.Run", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            new CallTreeBuildRequest(scenario.Solution, symbol!, 1, 2, CallTreeDirection.Both),
            CancellationToken.None);

        Assert.False(truncated);
        Assert.Equal(2, root.Children.Count);
        Assert.StartsWith("[incoming] Caller.Invoke", root.Children[0].Name, System.StringComparison.Ordinal);
        Assert.StartsWith("[outgoing]", root.Children[1].Name, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildTreeAsync_Outgoing_ResolvesMemberGroupWhenCandidateSymbolsPresent()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec("OverloadProbe", [
            ("Calls.cs", """
                namespace OverloadProbe;
                public class Callee
                {
                    public void Work(int x) { }
                    public void Work(string s) { }
                }
                public class Caller
                {
                    public void Run()
                    {
                        var callee = new Callee();
                        callee.Work(42);
                    }
                }
                """)
        ]));
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            scenario.Solution, "OverloadProbe.Caller.Run", CancellationToken.None);
        Assert.NotNull(symbol);

        var (root, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
            new CallTreeBuildRequest(scenario.Solution, symbol!, 1, 10, CallTreeDirection.Outgoing),
            CancellationToken.None);

        Assert.False(truncated);
        Assert.Contains(root.Children, child => child.Name.Contains("Callee.Work", System.StringComparison.Ordinal));
    }
}
