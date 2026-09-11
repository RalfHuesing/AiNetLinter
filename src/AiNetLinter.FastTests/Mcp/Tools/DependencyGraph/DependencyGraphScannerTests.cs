#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DependencyGraph;

/// <summary>
/// Tests fuer <see cref="DependencyGraphScanner"/> — kleine, gezielte In-Memory-Solutions
/// (virtuelle <see cref="Solution"/>-Snapshots) statt der geteilten Live-Fixture, damit
/// jeder Test genau das Datei-Abhaengigkeits-Szenario aufbaut, das er pruefen will (Zyklen,
/// Multi-Typ-Dateien, BCL-Rauschen).
/// </summary>
[Trait("Category", "Component")]
public sealed class DependencyGraphScannerTests
{
    [Fact]
    public async Task ScanFileAsync_OutgoingOnly_ReturnsEdgeToReferencedFile()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B {}"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("FileA.cs", edge.From);
        Assert.Equal("FileB.cs", edge.To);
        Assert.Equal("outgoing", edge.Direction);
        Assert.Contains("B", edge.TypeNames);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ScanFileAsync_IncomingOnly_ReturnsEdgeFromReferencingFile()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B {}"));
        var solution = testSolution.Solution;
        var docB = GetDocument(solution, "FileB.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docB, new DependencyGraphScanRequest(solution, false, true, 1, 50), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("FileA.cs", edge.From);
        Assert.Equal("FileB.cs", edge.To);
        Assert.Equal("incoming", edge.Direction);
        Assert.Contains("B", edge.TypeNames);
    }

    [Fact]
    public async Task ScanFileAsync_ProductionAndTestReferencers_ProductionEdgeSortsBeforeTestEdgeWhenTruncated()
    {
        // Produktionskanten werden vor Testkanten priorisiert, damit maxResults=1 bei einer
        // Blast-Radius-Abfrage die fachlich relevante Produktionskante zeigt.
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A {}"),
            ("ZZZProd.cs", "public class ZZZProd { public A? Other; }"),
            ("MyProject.Tests/AAATest.cs", "public class AAATest { public A? Other; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, false, true, 1, 1), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("ZZZProd.cs", edge.From);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanFileAsync_ProductionScope_ExcludesTestsBeforeLimitAndCountsExclusions()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A {}"),
            ("ZZZProd.cs", "public class ZZZProd { public A? Other; }"),
            ("MyProject.Tests/AAATest.cs", "public class AAATest { public A? Other; }"));
        var solution = testSolution.Solution;

        var result = await DependencyGraphScanner.ScanFileAsync(
            GetDocument(solution, "FileA.cs"),
            new DependencyGraphScanRequest(
                solution,
                false,
                true,
                1,
                1,
                McpScopeType.Production,
                IncludeGenerated: false),
            CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("ZZZProd.cs", edge.From);
        Assert.Equal(1, result.TotalEdgeCount);
        Assert.Equal(1, result.ExcludedEdgeCount);
        Assert.Equal(1, result.ExcludedNodeCount);
        Assert.All(result.Nodes!, node => Assert.Equal("production", node.ScopeType));
    }

    [Fact]
    public async Task ScanFileAsync_ProductionScope_KeepsNecessaryGeneratedBridgeMarked()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public Generated? Value; }"),
            ("Generated.g.cs", "public class Generated { public ProdB? Value; }"),
            ("ProdB.cs", "public class ProdB {}"));
        var solution = testSolution.Solution;

        var result = await DependencyGraphScanner.ScanFileAsync(
            GetDocument(solution, "FileA.cs"),
            new DependencyGraphScanRequest(
                solution,
                true,
                false,
                2,
                50,
                McpScopeType.Production,
                IncludeGenerated: false),
            CancellationToken.None);

        Assert.Contains(result.Edges, edge => edge.From == "FileA.cs" && edge.To == "Generated.g.cs");
        Assert.Contains(result.Edges, edge => edge.From == "Generated.g.cs" && edge.To == "ProdB.cs");
        var bridge = Assert.Single(result.Nodes!.Where(node => node.Path == "Generated.g.cs"));
        Assert.Equal("production", bridge.ScopeType);
        Assert.Equal("generated", bridge.SourceKind);
        Assert.True(bridge.IsBridge);
    }

    [Fact]
    public async Task ScanFileAsync_ProductionScope_WithGeneratedOptIn_ExcludesTestGeneratedFiles()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public ProdGenerated? Prod; public TestGenerated? Test; }"),
            ("ProdGenerated.g.cs", "public class ProdGenerated {}"),
            ("MyProject.Tests/TestGenerated.g.cs", "public class TestGenerated {}"));
        var solution = testSolution.Solution;

        var result = await DependencyGraphScanner.ScanFileAsync(
            GetDocument(solution, "FileA.cs"),
            new DependencyGraphScanRequest(
                solution,
                true,
                false,
                1,
                50,
                McpScopeType.Production,
                IncludeGenerated: true),
            CancellationToken.None);

        var generated = Assert.Single(result.Edges);
        Assert.Equal("ProdGenerated.g.cs", generated.To);
        Assert.DoesNotContain(result.Edges, edge => edge.To == "MyProject.Tests/TestGenerated.g.cs");
        Assert.All(result.Nodes!, node => Assert.Equal("production", node.ScopeType));
    }

    [Fact]
    public async Task ScanFileAsync_TestScope_WithGeneratedOptIn_ExcludesProductionGeneratedFiles()
    {
        using var testSolution = CreateSolution(
            ("MyProject.Tests/TestA.cs", "public class TestA { public ProdGenerated? Prod; public TestGenerated? Test; }"),
            ("ProdGenerated.g.cs", "public class ProdGenerated {}"),
            ("MyProject.Tests/TestGenerated.g.cs", "public class TestGenerated {}"));
        var solution = testSolution.Solution;

        var result = await DependencyGraphScanner.ScanFileAsync(
            GetDocument(solution, "MyProject.Tests/TestA.cs"),
            new DependencyGraphScanRequest(
                solution,
                true,
                false,
                1,
                50,
                McpScopeType.Tests,
                IncludeGenerated: true),
            CancellationToken.None);

        var generated = Assert.Single(result.Edges);
        Assert.Equal("MyProject.Tests/TestGenerated.g.cs", generated.To);
        Assert.DoesNotContain(result.Edges, edge => edge.To == "ProdGenerated.g.cs");
        Assert.All(result.Nodes!, node => Assert.Equal("tests", node.ScopeType));
    }

    [Fact]
    public async Task ScanFileAsync_MaxResults_ReportsShownAndTotalNodesSeparately()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? B; public C? C; }"),
            ("FileB.cs", "public class B {}"),
            ("FileC.cs", "public class C {}"));
        var solution = testSolution.Solution;

        var result = await DependencyGraphScanner.ScanFileAsync(
            GetDocument(solution, "FileA.cs"),
            new DependencyGraphScanRequest(solution, true, false, 1, 1),
            CancellationToken.None);

        Assert.Equal(3, result.TotalNodeCount);
        Assert.Equal(2, result.ShownNodeCount);
        Assert.Equal(1, result.Edges.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanFileAsync_BothDirections_ReturnsBothEdgeSets()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? Out; }"),
            ("FileB.cs", "public class B {}"),
            ("FileC.cs", "public class C { public A? In; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 1, 50), CancellationToken.None);

        Assert.Equal(2, result.Edges.Count);
        Assert.Contains(result.Edges, e => e.Direction == "outgoing" && e.From == "FileA.cs" && e.To == "FileB.cs");
        Assert.Contains(result.Edges, e => e.Direction == "incoming" && e.From == "FileC.cs" && e.To == "FileA.cs");
    }

    [Fact]
    public async Task ScanFileAsync_NoDependencies_ReturnsEmptyResultNotError()
    {
        using var testSolution = CreateSolution(("FileA.cs", "public class A { public int X; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
        Assert.Equal(0, result.TotalEdgeCount);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ScanFileAsync_CyclicFiles_DepthTwo_TerminatesAndBothDirectionsAppear()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B { public A? Other; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, false, 2, 50), CancellationToken.None);

        // Kein Duplikat-Explosion: genau zwei Kanten (A->B aus Hop 1, B->A aus Hop 2), die
        // schliessende Kante des Zyklus bleibt sichtbar statt still verworfen zu werden.
        Assert.Equal(2, result.Edges.Count);
        Assert.Contains(result.Edges, e => e.From == "FileA.cs" && e.To == "FileB.cs");
        Assert.Contains(result.Edges, e => e.From == "FileB.cs" && e.To == "FileA.cs");
        Assert.False(result.NodeCapReached);
    }

    [Fact]
    public async Task ScanFileAsync_MaxResultsBelowTotal_TruncatesEdges()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? B; public C? C; public D? D; public E? E; }"),
            ("FileB.cs", "public class B {}"),
            ("FileC.cs", "public class C {}"),
            ("FileD.cs", "public class D {}"),
            ("FileE.cs", "public class E {}"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, false, 1, 2), CancellationToken.None);

        Assert.Equal(4, result.TotalEdgeCount);
        Assert.Equal(2, result.Edges.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanFileAsync_DepthAboveCap_ClampsToThree()
    {
        using var testSolution = CreateSolution(("FileA.cs", "public class A { public int X; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 99, 50), CancellationToken.None);

        Assert.Equal(DependencyGraphScanner.MaxDepth, result.ClampedDepth);
    }

    [Fact]
    public async Task ScanTypeAsync_Incoming_NarrowerThanFile_ExcludesOtherTypeReferences()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class TypeOne {} public class TypeTwo {}"),
            ("FileB.cs", "public class UsesOne { public TypeOne? Prop; }"),
            ("FileC.cs", "public class UsesTwo { public TypeTwo? Prop; }"));
        var solution = testSolution.Solution;
        var typeOne = await GetTypeSymbolAsync(solution, "FileA.cs", "TypeOne");

        var result = await DependencyGraphScanner.ScanTypeAsync(
            typeOne, new DependencyGraphScanRequest(solution, false, true, 1, 50), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("FileB.cs", edge.From);
        Assert.DoesNotContain(result.Edges, e => e.From == "FileC.cs");
    }

    [Fact]
    public async Task ScanTypeAsync_Outgoing_ReturnsOnlyThatTypesReferences()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class TypeOne { public Helper? H; } public class TypeTwo { public Helper? H; }"),
            ("FileB.cs", "public class Helper {}"));
        var solution = testSolution.Solution;
        var typeOne = await GetTypeSymbolAsync(solution, "FileA.cs", "TypeOne");

        var result = await DependencyGraphScanner.ScanTypeAsync(
            typeOne, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal("FileB.cs", edge.To);
        Assert.Contains("Helper", edge.TypeNames);
    }

    [Fact]
    public async Task ScanTypeAsync_Outgoing_SelfReferencingType_ExcludesSelfEdge()
    {
        using var testSolution = CreateSolution(("Node.cs", "public class Node { public Node? Next; }"));
        var solution = testSolution.Solution;
        var node = await GetTypeSymbolAsync(solution, "Node.cs", "Node");

        var result = await DependencyGraphScanner.ScanTypeAsync(
            node, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_IntraFileReferences_ExcludedAsSelfEdges()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "public class A { public B? Other; } public class B { public A? Other; }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_OutgoingBclTypeReference_ExcludedFromEdges()
    {
        using var testSolution = CreateSolution(
            ("FileA.cs", "using System.Collections.Generic; public class A { public List<int> Items = new(); }"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_IncomingMultipleReferencesSameFile_AggregatesReferenceCount()
    {
        using var testSolution = CreateSolution(
            ("FileB.cs", "public class B {}"),
            ("FileA.cs", "public class A { public B? X; public B? Y; }"));
        var solution = testSolution.Solution;
        var docB = GetDocument(solution, "FileB.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docB, new DependencyGraphScanRequest(solution, false, true, 1, 50), CancellationToken.None);

        var edge = Assert.Single(result.Edges);
        Assert.Equal(2, edge.ReferenceCount);
        Assert.Equal(new[] { "B" }, edge.TypeNames);
    }

    [Fact]
    public async Task ScanFileAsync_SingleProjectAdhocSolution_ProjectReferencesEmptyNotCrash()
    {
        using var testSolution = CreateSolution(("FileA.cs", "public class A {}"));
        var solution = testSolution.Solution;
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 1, 50), CancellationToken.None);

        Assert.Empty(result.ProjectReferences);
    }

    // --- Test-Infrastruktur ---

    private static Document GetDocument(Solution solution, string fileName) =>
        solution.Projects.Single().Documents.Single(d => d.Name == fileName);

    private static async Task<INamedTypeSymbol> GetTypeSymbolAsync(Solution solution, string fileName, string typeName)
    {
        var document = GetDocument(solution, fileName);
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        var decl = root!.DescendantNodes().OfType<TypeDeclarationSyntax>().Single(t => t.Identifier.Text == typeName);
        return (INamedTypeSymbol)semanticModel!.GetDeclaredSymbol(decl)!;
    }

    private static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DependencyGraphScannerTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: "."));
}
