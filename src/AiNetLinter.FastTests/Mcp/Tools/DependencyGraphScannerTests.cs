#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

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
        // Regression: "MyProject.Tests/..." sortiert ordinal VOR "ZZZProd.cs" ('.' < 'Z' as
        // Zeichen ist hier nicht der Punkt, sondern dass Testpfade zufaellig alphabetisch frueher
        // liegen koennen als Produktionscode) — ohne die Test-Projekt-Nachrangigkeit wuerde
        // maxResults=1 die alphabetisch fruehere Test-Kante zeigen statt der fuer die
        // Blast-Radius-Frage relevanteren Produktionscode-Kante.
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
