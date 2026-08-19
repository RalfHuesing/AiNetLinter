#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.FastTests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="DependencyGraphTool"/> — Argument-Validierung (gegenseitig exklusive
/// Parameter, ungueltige <c>direction</c>), Fehler-Delegation (Datei/Typ nicht gefunden) und
/// End-zu-End-Wiring gegen die geteilte SymbolGraphMini-Fixture (Caller.cs -&gt; Greeter.cs ist
/// eine echte, deterministische Datei-Abhaengigkeit ueber <c>new Greeter()</c>).
/// </summary>
[Trait("Category", "Component")]
public sealed class DependencyGraphToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public DependencyGraphToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("irrelevant.cs", null, null, 1, 50), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BothFilePathAndTypeIdentifierGiven_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/Greeter.cs", "Greeter", null, 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("filePath ODER symbolIdentifier", textContent.Text);
        Assert.DoesNotContain("gitRef", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NeitherFilePathNorTypeIdentifierGiven_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput(null, null, null, 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("filePath ODER symbolIdentifier", textContent.Text);
        Assert.DoesNotContain("gitRef", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilePath_ReturnsRecoverableResourceNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/DoesNotExist.cs", null, null, 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTypeIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput(null, "DoesNotExistXyz", null, 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDirection_ReturnsRecoverableInvalidArgumentListingValidValues()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/Greeter.cs", null, "sideways", 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("incoming", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("outgoing", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("both", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FilePathGiven_OutgoingSectionContainsGreeterFile()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/Caller.cs", null, "outgoing", 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Ausgehende Abhaengigkeiten", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeIdentifierGiven_IncomingSectionContainsCallerFile()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput(null, "Greeter", "incoming", 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Eingehende Abhaengigkeiten", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredContent_IsJsonObjectNotArray()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/Caller.cs", null, "outgoing", 1, 50), CancellationToken.None);

        Assert.NotNull(result.StructuredContent);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Array, result.StructuredContent.Value.GetProperty("edges").ValueKind);
    }

    [Fact]
    public async Task ExecuteAsync_NotTruncated_AppendsSufficiencyHint()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/Caller.cs", null, "outgoing", 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FileWithoutDependencies_ReturnsEmptySectionsNotError()
    {
        var state = _fixture.CreateServer();

        var result = await DependencyGraphTool.ExecuteAsync(
            state, new DependencyGraphInput("src/SymbolGraphMini/OtherCaller.cs", null, "both", 1, 50), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("(keine)", textContent.Text, StringComparison.Ordinal);
    }
}
