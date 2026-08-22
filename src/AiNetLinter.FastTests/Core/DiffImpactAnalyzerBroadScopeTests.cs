#nullable enable

using System;
using System.Collections.Generic;
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
public sealed class DiffImpactAnalyzerBroadScopeTests
{
    [Fact]
    public async Task ChangeContext_ReportsPrivateMethodThatCallersOmits()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "Host.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Host
                    {
                        public int Add(int a, int b) => a + b;

                        private int Audit(int value) => value * 2;
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var changeContext = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(7, 1));
        var callers = await ScanAsync(document, DiffSymbolScope.Callers, new HunkRange(7, 1));

        var entry = Assert.Single(changeContext);
        Assert.Equal("M:ScenarioNs.Host.Audit(System.Int32)~System.Int32", entry.SymbolId);
        Assert.Equal("Host.Audit", entry.DisplayName);
        Assert.Equal("Method", entry.Kind);
        Assert.Equal(Accessibility.Private, entry.Accessibility);
        Assert.Empty(callers);
    }

    [Fact]
    public async Task ChangeContext_BodyHunk_ReportsInnermostMethodWithoutContainingType()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "Host.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Host
                    {
                        public int Compute(int input)
                        {
                            return input * 3;
                        }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var entries = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(7, 1));

        var entry = Assert.Single(entries);
        Assert.Equal("Host.Compute", entry.DisplayName);
        Assert.Equal("Method", entry.Kind);
    }

    [Fact]
    public async Task ChangeContext_HunkInsideLocalFunction_ReportsOnlyTheLocalFunction()
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
                            int Scale(int value)
                            {
                                return value * 2;
                            }

                            return Scale(input);
                        }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var entries = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(9, 1));

        var entry = Assert.Single(entries);
        Assert.Equal("LocalFuncHost.Run.Scale", entry.DisplayName);
        Assert.Equal("Method", entry.Kind);
        // Accessibility unveraendert aus dem Symbol: Roslyn liefert fuer lokale Funktionen
        // DeclaredAccessibility=Private (nicht NotApplicable).
        Assert.Equal(Accessibility.Private, entry.Accessibility);
        Assert.Contains("#lf:", entry.SymbolId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeContext_PropertyGetterHunk_ReportsExactlyOnePropertyEntry()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "Host.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Host
                    {
                        public int Value
                        {
                            get { return _value; }
                            set { _value = value; }
                        }

                        private int _value;
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var entries = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(7, 1));

        var entry = Assert.Single(entries);
        Assert.Equal("Host.Value", entry.DisplayName);
        Assert.Equal("Property", entry.Kind);
        Assert.Equal(Accessibility.Public, entry.Accessibility);
        Assert.Equal(5, entry.StartLine);
        Assert.Equal(9, entry.EndLine);
    }

    [Fact]
    public async Task ChangeContext_FieldInitializerChange_ReportsFieldEntryWithKindAndAccessibility()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "Host.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Host
                    {
                        private readonly int seed = 42;
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var entries = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(5, 1));

        var entry = Assert.Single(entries);
        Assert.Equal("Host.seed", entry.DisplayName);
        Assert.Equal("Field", entry.Kind);
        Assert.Equal(Accessibility.Private, entry.Accessibility);
    }

    [Fact]
    public async Task ChangeContext_EventDeclarationChange_ReportsEventEntry()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "Host.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Host
                    {
                        public event System.EventHandler Done;
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var entries = await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(5, 1));

        var entry = Assert.Single(entries);
        Assert.Equal("Host.Done", entry.DisplayName);
        Assert.Equal("Event", entry.Kind);
        Assert.Equal(Accessibility.Public, entry.Accessibility);
    }

    [Fact]
    public async Task ChangeContext_PartialTypeInTwoFiles_TwoEntriesDistinctByFileAndSpan_SameSymbolId()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "PartialOne.cs",
                Content: """
                    namespace ScenarioNs;

                    public partial class Part
                    {
                        public int Alpha() => 1;
                    }
                    """),
            (
                FileName: "PartialTwo.cs",
                Content: """
                    namespace ScenarioNs;

                    public partial class Part
                    {
                        public int Beta() => 2;

                        public int Gamma() => 3;
                    }
                    """)]));
        var documents = scenario.Solution.Projects.SelectMany(project => project.Documents).ToList();
        var partialOne = documents.Single(document => document.Name == "PartialOne.cs");
        var partialTwo = documents.Single(document => document.Name == "PartialTwo.cs");

        var entryOne = Assert.Single(await ScanAsync(partialOne, DiffSymbolScope.ChangeContext, new HunkRange(3, 1)));
        var entryTwo = Assert.Single(await ScanAsync(partialTwo, DiffSymbolScope.ChangeContext, new HunkRange(3, 1)));

        Assert.Equal("T:ScenarioNs.Part", entryOne.SymbolId);
        Assert.Equal(entryOne.SymbolId, entryTwo.SymbolId);
        Assert.Equal("ScenarioNs.Part", entryOne.DisplayName);
        Assert.NotEqual(entryOne.FilePath, entryTwo.FilePath);
        Assert.EndsWith("PartialOne.cs", entryOne.FilePath, StringComparison.Ordinal);
        Assert.EndsWith("PartialTwo.cs", entryTwo.FilePath, StringComparison.Ordinal);
        Assert.NotEqual((entryOne.StartLine, entryOne.EndLine), (entryTwo.StartLine, entryTwo.EndLine));
    }

    [Fact]
    public async Task GetStableSymbolId_TwoLocalFunctionsInOneMethod_DistinctDeterministicIdsWithLfMarker()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "IdHost.cs",
                Content: """
                    namespace ScenarioNs;

                    public static class IdHost
                    {
                        public static int Run(int input)
                        {
                            int Scale(int value) => value * 2;

                            int Offset(int value) => value + 1;

                            return Scale(input) + Offset(input);
                        }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();
        var semanticModel = await document.GetSemanticModelAsync();
        var localFunctions = semanticModel!.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<LocalFunctionStatementSyntax>().ToList();
        var scale = semanticModel.GetDeclaredSymbol(localFunctions[0])!;
        var offset = semanticModel.GetDeclaredSymbol(localFunctions[1])!;
        var hostMethodId = "M:ScenarioNs.IdHost.Run(System.Int32)~System.Int32";

        var scaleId = CallGraphTraversal.GetStableSymbolId(scale);
        var offsetId = CallGraphTraversal.GetStableSymbolId(offset);

        Assert.Equal($"{hostMethodId}#lf:Scale@7:13", scaleId);
        Assert.Equal($"{hostMethodId}#lf:Offset@9:13", offsetId);
        Assert.NotEqual(scaleId, offsetId);
        Assert.Equal(scaleId, CallGraphTraversal.GetStableSymbolId(scale));
        Assert.NotEqual(hostMethodId, scaleId);
        Assert.NotEqual(hostMethodId, offsetId);
    }

    [Fact]
    public async Task ChangeContext_DisplayNames_FollowTypeNestedAndLocalFunctionContract()
    {
        using var scenario = McpInMemoryTestContext.CreateScenario(new ProjectSpec(
            "ScenarioProj",
            [(
                FileName: "DisplayHost.cs",
                Content: """
                    namespace ScenarioNs;

                    public class Outer
                    {
                        public class Inner
                        {
                            public void Work()
                            {
                                void Helper()
                                {
                                }
                            }
                        }
                    }
                    """)]));
        var document = scenario.Solution.Projects.SelectMany(project => project.Documents).Single();

        var outer = Assert.Single(await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(3, 1)));
        var inner = Assert.Single(await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(5, 1)));
        var helper = Assert.Single(await ScanAsync(document, DiffSymbolScope.ChangeContext, new HunkRange(9, 1)));

        Assert.Equal("ScenarioNs.Outer", outer.DisplayName);
        Assert.Equal("NamedType", outer.Kind);
        Assert.Equal("Outer.Inner", inner.DisplayName);
        Assert.Equal("NamedType", inner.Kind);
        // Enthaltende Methode im bisherigen Mitgliedsschema („EnthaltenderTyp.Name“, ohne
        // Namensraum-Qualifikation): Work liegt direkt im verschachtelten Typ Inner.
        Assert.Equal("Inner.Work.Helper", helper.DisplayName);
        Assert.Equal("Method", helper.Kind);
    }

    private static async Task<List<ChangedSymbolEntry>> ScanAsync(
        Document document, DiffSymbolScope scope, params HunkRange[] ranges)
    {
        var matches = await DiffSymbolScanner.FindChangedSymbolsAsync(document, ranges, scope);
        return matches.Select(match => match.Entry).ToList();
    }
}
