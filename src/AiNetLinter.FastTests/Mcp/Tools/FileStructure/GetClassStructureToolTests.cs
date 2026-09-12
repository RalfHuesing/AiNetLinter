#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed partial class GetClassStructureToolTests
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
    public async Task ExecuteAsync_SymbolIdentifier_ResolvesClassStructure()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(
            state,
            new GetClassStructureArgs("Greeter", "lines"),
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
    public async Task ExecuteAsync_ValidClass_RendersTypeAndMembers()
    {
        var state = _fixture.CreateServer();

        var result = await GetClassStructureTool.ExecuteAsync(state, "Greeter", "lines", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("# Typ: SymbolGraphMini.Greeter", text, StringComparison.Ordinal);
        Assert.Contains("- Kind: class", text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.Contains("Greet", text, StringComparison.Ordinal);
        Assert.DoesNotContain("maxMembers erhöhen", text, StringComparison.Ordinal);
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

        var text = TextOf(result);
        Assert.Contains("1.5", text, StringComparison.Ordinal);
        Assert.Contains("-7", text, StringComparison.Ordinal);
        Assert.Contains("\"hello\"", text, StringComparison.Ordinal);
        Assert.Contains("null", text, StringComparison.Ordinal);
        Assert.Contains("'x'", text, StringComparison.Ordinal);
        Assert.Contains("true", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.True(text.IndexOf("Alpha", StringComparison.Ordinal) < text.IndexOf("Bravo", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Bravo", StringComparison.Ordinal) < text.IndexOf("Zulu", StringComparison.Ordinal));
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
        var text = TextOf(result);
        Assert.Contains("| Kind | Name | Visibility | File | Lines | LineCount | Signature |", text, StringComparison.Ordinal);
        Assert.Contains("MultiPart.A.cs", text, StringComparison.Ordinal);
        Assert.Contains("MultiPart.B.cs", text, StringComparison.Ordinal);
        Assert.Contains("MethodA", text, StringComparison.Ordinal);
        Assert.Contains("MethodB", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.Contains("von", text, StringComparison.Ordinal);
        Assert.Contains("maxMembers erhöhen", text, StringComparison.Ordinal);
        Assert.Contains("HiddenMethod", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFiltersPartialMembersAndKeepsMarkedSeedLocations()
    {
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GetClassStructureScopeTests.slnx",
            new ProjectSpec("App", [
                ("Shared.cs", "namespace TestNs; public partial class Shared { public void Production() { } }"),
                ("SharedTests.cs", "namespace TestNs; public partial class Shared { public void TestOnly() { } }"),
                ("Shared.generated.cs", "// <auto-generated />\nnamespace TestNs; public partial class Shared { public void GeneratedOnly() { } }")
            ], VirtualProjectDirectory: "src/App")));

        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("Shared", Scope: new McpScopeInput(McpScopeType.Production, IncludeGenerated: false)),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("Production", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TestOnly", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedOnly", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedTests.cs", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Shared.generated.cs", text, StringComparison.OrdinalIgnoreCase);
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
        var text = TextOf(result);
        Assert.Contains("TinyClass", text, StringComparison.Ordinal);
        Assert.Contains("A", text, StringComparison.Ordinal);
        Assert.DoesNotContain("maxMembers erhöhen", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.Contains("- Kind: record class", text, StringComparison.Ordinal);
        Assert.Contains("FirstName", text, StringComparison.Ordinal);
        Assert.Contains("LastName", text, StringComparison.Ordinal);
        Assert.Contains("Age", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.Contains("LongMethod", text, StringComparison.Ordinal);
        Assert.Contains("4-9", text, StringComparison.Ordinal);
        Assert.Contains("6", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.Contains("MethodA", text, StringComparison.Ordinal);
        Assert.Contains("MethodB", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MyProp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("_field", text, StringComparison.Ordinal);
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
        var text = TextOf(result);
        Assert.Contains("ProcessOrder", text, StringComparison.Ordinal);
        Assert.Contains("ProcessPayment", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CancelOrder", text, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
