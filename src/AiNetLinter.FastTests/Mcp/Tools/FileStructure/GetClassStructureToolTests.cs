#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class GetClassStructureToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetClassStructureToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetClassStructureTool.ExecuteAsync(state, "Greeter", "lines", CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSymbol_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolNotFound_ReturnsSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "DoesNotExistClass", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolAlias_ResolvesClassStructure()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(
            state,
            new GetClassStructureArgs(null, "lines", Symbol: "Greeter"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Typ: SymbolGraphMini.Greeter", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidClass_ReturnsHeaderAndMemberTable()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Greeter", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("# Typ: SymbolGraphMini.Greeter", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("- Kind: class", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("| Kind | Name | Visibility | Lines | LineCount | Signature |", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greet", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidClass_ReturnsStructuredContent()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Greeter", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("SymbolGraphMini.Greeter", payload!.TypeName);
        Assert.Equal("class", payload.Kind);
        Assert.NotEmpty(payload.Files);
        Assert.NotEmpty(payload.Members);
        Assert.Contains(payload.Members, m => m.Name == "Greet" && m.Kind == "Method");
        Assert.Equal(payload.TotalMemberCount, payload.ShownMemberCount);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task ExecuteAsync_ConstantFields_FormatsInvariantLiteralValues()
    {
        const string source = """
            namespace TestNs;
            public class Constants
            {
                public const double Ratio = 1.5;
                public const int Offset = -7;
                public const string Greeting = "hello";
                public const string? Missing = null;
                public const char Marker = 'x';
                public const bool Enabled = true;
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("Constants.cs", source)])));

        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(), "Constants", "name", CancellationToken.None);

        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Contains(payload!.Members, member => member.Signature.Contains("1.5", StringComparison.Ordinal));
        Assert.Contains(payload.Members, member => member.Signature.Contains("-7", StringComparison.Ordinal));
        Assert.Contains(payload.Members, member => member.Signature.Contains("\"hello\"", StringComparison.Ordinal));
        Assert.Contains(payload.Members, member => member.Signature.Contains("null", StringComparison.Ordinal));
        Assert.Contains(payload.Members, member => member.Signature.Contains("'x'", StringComparison.Ordinal));
        Assert.Contains(payload.Members, member => member.Signature.Contains("true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_SortByName_SortsAlphabetically()
    {
        const string source = """
            namespace TestNs;
            public class Sample
            {
                public void Zulu() { }
                public void Alpha() { }
                public void Bravo() { }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("Sample.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Sample", "name", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        var methodNames = payload!.Members.Where(m => m.Kind == "Method").Select(m => m.Name).ToList();
        Assert.Equal(new[] { "Alpha", "Bravo", "Zulu" }, methodNames);
    }

    [Fact]
    public async Task ExecuteAsync_PartialClass_CombinesMultipleFiles()
    {
        const string part1 = """
            namespace TestNs;
            public partial class MultiPart
            {
                public void MethodA() { }
            }
            """;
        const string part2 = """
            namespace TestNs;
            public partial class MultiPart
            {
                public void MethodB() { }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("MultiPart.A.cs", part1), ("MultiPart.B.cs", part2)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "MultiPart", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Files.Count);
        Assert.Equal(2, payload.Members.Count(m => m.Kind == "Method"));

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("| Kind | Name | Visibility | File | Lines | LineCount | Signature |", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("MultiPart.A.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("MultiPart.B.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxMembers_TruncatesMemberListAndSetsFlag()
    {
        // Klasse mit 60 privaten Methoden + 1 public Greet → 61 Member.
        // maxMembers=10 erwartet 10 Member + Truncated=true + Meta-Zeile im Markdown.
        var methods = string.Join("\n", Enumerable.Range(1, 60).Select(i => $"        private void HiddenMethod{i}() {{ }}"));
        var source = $$"""
            namespace TestNs;
            public class LargeClass
            {
                public void Greet() { }
            {{methods}}
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("LargeClass.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "LargeClass", "lines", maxMembers: 10, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        // Sanity: die Klasse muss tatsächlich 61 Member haben, sonst testet der Test nichts.
        Assert.True(payload!.TotalMemberCount >= 50, $"Test ungueltig: nur {payload.TotalMemberCount} Member gefunden (erwartet >= 50). Source:\n{source}");
        Assert.True(payload.Truncated, $"Truncated muss true sein bei {payload.TotalMemberCount} Member und maxMembers=10.");
        Assert.Equal(10, payload.ShownMemberCount);
        Assert.Equal(10, payload.Members.Count);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("maxMembers erhöhen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxMembers_ClampedToCap()
    {
        // User setzt maxMembers=10000 → muss auf 200 gekappt werden.
        // TinyClass hat impliziten Default-Constructor + 1 explizite Methode = 2 Member.
        const string source = """
            namespace TestNs;
            public class TinyClass { public void A() { } }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("TinyClass.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "TinyClass", "lines", maxMembers: 10000, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        // TinyClass hat 1 Method + 1 impliziten Default-Constructor; cap=200 ist irrelevant,
        // aber Clamp darf nicht crashen und nicht versehentlich truncaten.
        Assert.InRange(payload!.TotalMemberCount, 1, 3);
        Assert.Equal(payload.TotalMemberCount, payload.ShownMemberCount);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task ExecuteAsync_RecordWithPrimaryCtor_ListsParamsBeforeMembers()
    {
        const string source = """
            namespace TestNs;
            public record Person(string FirstName, string LastName, int Age);
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("Person.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Person", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("record class", payload!.Kind);
        var primaryCtorParams = payload.Members.Where(m => m.Kind == "PrimaryCtor-Param").ToList();
        // Default sortBy="lines" sortiert nach FilePath+StartLine — bei gleicher recordLine
        // ist die Reihenfolge der PrimaryCtor-Params also undefiniert, prüfen wir nur die
        // Anwesenheit der korrekten Param-Namen statt die Sortierung.
        Assert.Equal(3, primaryCtorParams.Count);
        Assert.Equal(
            new HashSet<string> { "FirstName", "LastName", "Age" },
            primaryCtorParams.Select(p => p.Name).ToHashSet());
        // Primäre Konstruktor-Parameter müssen vor den restlichen Membern stehen
        // (Equals/GetHashCode/ToString/PrintMembers-Boilerplate, der vom Compiler generiert wird).
        var firstNonParamIndex = payload.Members
            .Select((m, idx) => (m, idx))
            .First(t => t.m.Kind != "PrimaryCtor-Param").idx;
        var lastParamIndex = payload.Members
            .Select((m, idx) => (m, idx))
            .Last(t => t.m.Kind == "PrimaryCtor-Param").idx;
        Assert.True(lastParamIndex < firstNonParamIndex,
            $"PrimaryCtor-Params müssen vor den restlichen Membern stehen (last={lastParamIndex}, firstNonParam={firstNonParamIndex}).");
    }

    [Fact]
    public async Task ExecuteAsync_MultiLineMethod_CalculatesAccurateLineCountAndSpan()
    {
        const string source = """
            namespace TestNs;
            public class Service
            {
                public void LongMethod()
                {
                    var x = 1;
                    var y = 2;
                    var z = x + y;
                }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("Service.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Service", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        var method = Assert.Single(payload!.Members.Where(m => m.Name == "LongMethod"));
        Assert.Equal(4, method.StartLine);
        Assert.Equal(9, method.EndLine);
        Assert.Equal(6, method.LineCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithKindFilter_FiltersMembersByKind()
    {
        const string source = """
            namespace TestNs;
            public class MixedClass
            {
                public int MyProp { get; set; }
                public void MethodA() { }
                public void MethodB() { }
                private int _field;
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("MixedClass.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(
            state, new GetClassStructureArgs("MixedClass", "lines", MaxMembers: 50, KindFilter: "Method"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.All(payload!.Members, m => Assert.Equal("Method", m.Kind));
        Assert.Equal(2, payload.TotalMemberCount);
        Assert.Contains(payload.Members, m => m.Name == "MethodA");
        Assert.Contains(payload.Members, m => m.Name == "MethodB");
    }

    [Fact]
    public async Task ExecuteAsync_WithNameFilter_FiltersMembersByName()
    {
        const string source = """
            namespace TestNs;
            public class MultiMethodClass
            {
                public void ProcessOrder() { }
                public void ProcessPayment() { }
                public void CancelOrder() { }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureToolTests.slnx",
            new ProjectSpec("TestProject", [("MultiMethodClass.cs", source)])));
        var state = context.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(
            state, new GetClassStructureArgs("MultiMethodClass", "lines", MaxMembers: 50, NameFilter: "Process"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.TotalMemberCount);
        Assert.Contains(payload.Members, m => m.Name == "ProcessOrder");
        Assert.Contains(payload.Members, m => m.Name == "ProcessPayment");
        Assert.DoesNotContain(payload.Members, m => m.Name == "CancelOrder");
    }
}
