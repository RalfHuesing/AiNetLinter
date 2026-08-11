#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="DependencyGraphScanner"/> — kleine, gezielte In-Memory-Solutions
/// (<see cref="AdhocWorkspace"/>, Dateien real auf Platte gespiegelt, Pattern uebernommen von
/// <c>PatternDetectScannerTests.CreateAdhocSolution</c>) statt der geteilten Live-Fixture, damit
/// jeder Test genau das Datei-Abhaengigkeits-Szenario aufbaut, das er pruefen will (Zyklen,
/// Multi-Typ-Dateien, BCL-Rauschen).
/// </summary>
[Trait("Category", "Unit")]
public sealed class DependencyGraphScannerTests
{
    [Fact]
    public async Task ScanFileAsync_OutgoingOnly_ReturnsEdgeToReferencedFile()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B {}"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B {}"));
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
    public async Task ScanFileAsync_BothDirections_ReturnsBothEdgeSets()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? Out; }"),
            ("FileB.cs", "public class B {}"),
            ("FileC.cs", "public class C { public A? In; }"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("FileA.cs", "public class A { public int X; }"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? Other; }"),
            ("FileB.cs", "public class B { public A? Other; }"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? B; public C? C; public D? D; public E? E; }"),
            ("FileB.cs", "public class B {}"),
            ("FileC.cs", "public class C {}"),
            ("FileD.cs", "public class D {}"),
            ("FileE.cs", "public class E {}"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("FileA.cs", "public class A { public int X; }"));
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 99, 50), CancellationToken.None);

        Assert.Equal(DependencyGraphScanner.MaxDepth, result.ClampedDepth);
    }

    [Fact]
    public async Task ScanTypeAsync_Incoming_NarrowerThanFile_ExcludesOtherTypeReferences()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class TypeOne {} public class TypeTwo {}"),
            ("FileB.cs", "public class UsesOne { public TypeOne? Prop; }"),
            ("FileC.cs", "public class UsesTwo { public TypeTwo? Prop; }"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class TypeOne { public Helper? H; } public class TypeTwo { public Helper? H; }"),
            ("FileB.cs", "public class Helper {}"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("Node.cs", "public class Node { public Node? Next; }"));
        var node = await GetTypeSymbolAsync(solution, "Node.cs", "Node");

        var result = await DependencyGraphScanner.ScanTypeAsync(
            node, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_IntraFileReferences_ExcludedAsSelfEdges()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "public class A { public B? Other; } public class B { public A? Other; }"));
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, true, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_OutgoingBclTypeReference_ExcludedFromEdges()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileA.cs", "using System.Collections.Generic; public class A { public List<int> Items = new(); }"));
        var docA = GetDocument(solution, "FileA.cs");

        var result = await DependencyGraphScanner.ScanFileAsync(
            docA, new DependencyGraphScanRequest(solution, true, false, 1, 50), CancellationToken.None);

        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ScanFileAsync_IncomingMultipleReferencesSameFile_AggregatesReferenceCount()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("FileB.cs", "public class B {}"),
            ("FileA.cs", "public class A { public B? X; public B? Y; }"));
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
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("FileA.cs", "public class A {}"));
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

    /// <summary>
    /// Baut eine In-Memory-Solution mit auf der Platte real gespiegelten Quelldateien —
    /// Pattern 1:1 uebernommen von <c>PatternDetectScannerTests.CreateAdhocSolution</c>: der
    /// Scanner leitet Solution-relative Pfade aus <c>solution.FilePath</c> ab (<c>Path.GetRelativePath</c>
    /// wirft bei leerem <c>relativeTo</c>), daher braucht auch die reine In-Memory-AdhocWorkspace
    /// hier ein explizites <c>SolutionInfo.FilePath</c>.
    /// </summary>
    private static Solution CreateAdhocSolution(string baseDir, params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solutionInfo = SolutionInfo.Create(
            SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(baseDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);

        foreach (var file in files)
        {
            var fullPath = Path.Combine(baseDir, file.fileName);
            File.WriteAllText(fullPath, file.content);
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, SourceText.From(file.content), filePath: fullPath);
        }
        return solution;
    }

    private sealed class TempSourceDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ainetlinter-dependencygraph-" + Guid.NewGuid().ToString("N"));

        public TempSourceDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
