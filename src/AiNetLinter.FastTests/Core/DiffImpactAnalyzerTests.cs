#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class DiffImpactAnalyzerTests
{
    [Fact]
    public void ParseGitDiffHunks_WithValidDiff_ParsesHunksCorrectly()
    {
        const string diffOutput = """
            diff --git a/src/FileA.cs b/src/FileA.cs
            --- a/src/FileA.cs
            +++ b/src/FileA.cs
            @@ -12,3 +45,5 @@ public class FileA
            + added line 1
            + added line 2
            + added line 3
            diff --git a/src/FileB.cs b/src/FileB.cs
            --- a/src/FileB.cs
            +++ b/src/FileB.cs
            @@ -10 +20 @@ public class FileB
            """;

        var result = DiffImpactAnalyzer.ParseGitDiffHunks(diffOutput);

        var keyA = Path.Combine("src", "FileA.cs");
        var keyB = Path.Combine("src", "FileB.cs");

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(keyA));
        Assert.True(result.ContainsKey(keyB));

        var fileALines = result[keyA];
        Assert.Equal(5, fileALines.Count);
        Assert.Contains(45, fileALines);
        Assert.Contains(49, fileALines);

        var fileBLines = result[keyB];
        Assert.Single(fileBLines);
        Assert.Contains(20, fileBLines);
    }

    [Fact]
    public void ParseGitDiffHunkRanges_WithMultiFileDiff_ParsesCompactRangesPerFile()
    {
        const string diffOutput = """
            diff --git a/src/FileA.cs b/src/FileA.cs
            --- a/src/FileA.cs
            +++ b/src/FileA.cs
            @@ -12,3 +45,5 @@ public class FileA
            + added line 1
            diff --git a/src/FileA.cs b/src/FileA.cs
            --- a/src/FileA.cs
            +++ b/src/FileA.cs
            @@ -30,1 +80,2 @@ public class FileA
            + more
            diff --git a/src/FileB.cs b/src/FileB.cs
            --- a/src/FileB.cs
            +++ b/src/FileB.cs
            @@ -10,2 +20,4 @@ public class FileB
            + x
            """;

        var result = DiffImpactAnalyzer.ParseGitDiffHunkRanges(diffOutput);

        var keyA = Path.Combine("src", "FileA.cs");
        var keyB = Path.Combine("src", "FileB.cs");

        Assert.Equal(2, result.Count);
        Assert.Equal(
            [new HunkRange(45, 5), new HunkRange(80, 2)],
            result[keyA]);
        Assert.Equal([new HunkRange(20, 4)], result[keyB]);
    }

    [Fact]
    public void ParseGitDiffHunkRanges_WithSingleLineAndZeroCountHunks_MapsRangesExactly()
    {
        const string diffOutput = """
            diff --git a/src/Only.cs b/src/Only.cs
            --- a/src/Only.cs
            +++ b/src/Only.cs
            @@ -10 +20 @@
            + added
            @@ -5,3 +7,0 @@
            - removed entirely
            """;

        var result = DiffImpactAnalyzer.ParseGitDiffHunkRanges(diffOutput);

        var key = Path.Combine("src", "Only.cs");
        Assert.Equal(
            [new HunkRange(20, 1), new HunkRange(7, 0)],
            result[key]);
    }

    [Fact]
    public void ExpandHunkRanges_ProducesLegacyExpandedLines()
    {
        const string diffOutput = """
            diff --git a/src/A.cs b/src/A.cs
            --- a/src/A.cs
            +++ b/src/A.cs
            @@ -12,3 +45,5 @@ public class A
            + a1
            + a2
            diff --git a/src/B.cs b/src/B.cs
            --- a/src/B.cs
            +++ b/src/B.cs
            @@ -10 +20 @@
            + b1
            @@ -5,3 +9,0 @@
            - gone
            """;

        var legacy = DiffImpactAnalyzer.ParseGitDiffHunks(diffOutput);
        var fromRanges = DiffImpactAnalyzer.ParseGitDiffHunkRanges(diffOutput)
            .ToDictionary(
                pair => pair.Key,
                pair => DiffImpactAnalyzer.ExpandHunkRanges(pair.Value));

        Assert.Equal(legacy, fromRanges);
        Assert.Equal(new List<int> { 20 }, fromRanges[Path.Combine("src", "B.cs")]);
        Assert.Empty(fromRanges[Path.Combine("src", "B.cs")].Skip(1));
    }

    [Fact]
    public async Task CreateChangedSymbolEntry_ForMethodSymbols_CarriesIdAccessibilityKindAndSpan()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "SampleClass.cs",
                Content: """
                    namespace ScenarioNs;

                    public class SampleClass
                    {
                        public int Add(int a, int b) => a + b;
                        internal void Refresh() { }
                        protected void OnTick() { }
                        private void Audit() { }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();
        var semanticModel = await document.GetSemanticModelAsync();

        var entries = ResolveMethodNodes(semanticModel!, ["Add", "Refresh", "OnTick", "Audit"])
            .Select(node => DiffImpactAnalyzer.CreateChangedSymbolEntry(
                semanticModel!.GetDeclaredSymbol(node)!, document))
            .ToList();

        var expected = new (string Name, string SymbolId, Accessibility Access, int Line)[]
        {
            ("Add", "M:ScenarioNs.SampleClass.Add(System.Int32,System.Int32)~System.Int32", Accessibility.Public, 5),
            ("Refresh", "M:ScenarioNs.SampleClass.Refresh", Accessibility.Internal, 6),
            ("OnTick", "M:ScenarioNs.SampleClass.OnTick", Accessibility.Protected, 7),
            ("Audit", "M:ScenarioNs.SampleClass.Audit", Accessibility.Private, 8),
        };

        Assert.Equal(expected.Length, entries.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var entry = entries[i];
            Assert.Equal(expected[i].SymbolId, entry.SymbolId);
            Assert.Equal(expected[i].Access, entry.Accessibility);
            Assert.Equal("Method", entry.Kind);
            Assert.Equal("SampleClass." + expected[i].Name, entry.DisplayName);
            Assert.Equal("ScenarioProj", entry.ProjectName);
            Assert.Equal("ScenarioProj/SampleClass.cs", entry.FilePath);
            Assert.Equal(expected[i].Line, entry.StartLine);
            Assert.Equal(expected[i].Line, entry.EndLine);
        }
    }

    [Fact]
    public async Task CreateChangedSymbolEntry_ForLocalFunction_UsesSharedStableIdLogic()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "LocalFuncHost.cs",
                Content: """
                    namespace ScenarioNs;

                    public static class LocalFuncHost
                    {
                        public static int Run(int input)
                        {
                            int Scale(int value) => value * 2;
                            return Scale(input);
                        }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();
        var semanticModel = await document.GetSemanticModelAsync();
        var localFunction = (LocalFunctionStatementSyntax)semanticModel!.SyntaxTree.GetRoot()
            .DescendantNodes().Single(node => node is LocalFunctionStatementSyntax);
        var symbol = semanticModel.GetDeclaredSymbol(localFunction)!;
        var fallbackId = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var entry = DiffImpactAnalyzer.CreateChangedSymbolEntry(symbol, document);
        var secondEntry = DiffImpactAnalyzer.CreateChangedSymbolEntry(symbol, document);

        // Eine Quelle der Wahrheit: identische ID wie die Traversal-Logik, deterministisch bei
        // Wiederholung. Lokale Funktionen nehmen dabei den DocCommentId-Pfad (nicht den
        // FullyQualified-Fallback).
        Assert.Equal(CallGraphTraversal.GetStableSymbolId(symbol), entry.SymbolId);
        Assert.Equal(entry.SymbolId, secondEntry.SymbolId);
        Assert.NotEqual(fallbackId, entry.SymbolId);
        Assert.Equal("Method", entry.Kind);
    }

    [Fact]
    public void ToCallSiteEntries_MapsReferencesCallSitesOrderAndFieldsIdentically()
    {
        // Bewusst unsortiert und mit Doppel-Eintrag: die Abbildung darf weder sortieren noch deduplizieren.
        var references = new ReferenceTraversalResult(
            new List<TransitiveCallSiteEntry>
            {
                new("b/Zwei.cs", 22, "Zwei.Zwei", "ProjektZwei", 1, "M:Eins"),
                new("a/Eins.cs", 11, "Eins.Eins", "ProjektEins", 1, "M:Wurzel"),
                new("a/Eins.cs", 11, "Eins.Eins", "ProjektEins", 1, "M:Wurzel"),
            },
            new TraversalCompleteness(1, 1, 1, 3, 3, false, false, false));

        var mapped = DiffImpactAnalyzer.ToCallSiteEntries(references);

        Assert.Equal(3, mapped.Count);
        Assert.Equal(
            [("b/Zwei.cs", 22, "Zwei.Zwei", "ProjektZwei"),
             ("a/Eins.cs", 11, "Eins.Eins", "ProjektEins"),
             ("a/Eins.cs", 11, "Eins.Eins", "ProjektEins")],
            mapped.Select(entry => (entry.FilePath, entry.Line, entry.SymbolName, entry.ProjectName)));
    }

    private static List<MethodDeclarationSyntax> ResolveMethodNodes(
        SemanticModel semanticModel, IReadOnlyList<string> names)
    {
        var root = semanticModel.SyntaxTree.GetRoot();
        return names.Select(name => (MethodDeclarationSyntax)root.DescendantNodes()
                .Single(node => node is MethodDeclarationSyntax method && method.Identifier.Text == name))
            .ToList();
    }
}
