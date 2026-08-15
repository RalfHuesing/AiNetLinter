#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

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
    }
}
