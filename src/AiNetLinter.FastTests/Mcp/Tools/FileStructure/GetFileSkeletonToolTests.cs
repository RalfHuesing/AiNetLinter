#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

// @covers FileSkeletonPayload
[Trait("Category", "Component")]
public sealed class GetFileSkeletonToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetFileSkeletonToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetFileSkeletonTool.ExecuteAsync(state, ["irrelevant.cs"], CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    public static IEnumerable<object?[]> EmptyCases =>
    [
        [null],
        [System.Array.Empty<string>()],
        [new[] { "", "   " }]
    ];

    [Theory]
    [MemberData(nameof(EmptyCases))]
    public async Task ExecuteAsync_EmptyFilePaths_ReturnsRecoverableInvalidArgument(string[]? filePaths)
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(state, filePaths, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("Pflichtparameter 'filePaths' fehlt oder ist leer.", textContent.Text);
        Assert.Contains("filePaths: [\"src/MyClass.cs\"]", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilePath_ReturnsRecoverableResourceNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/DoesNotExist.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsGreeterSkeletonWithGreetMethod()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Erzeugt:", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Caller", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OtherCaller", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRelativePath_ReturnsStructuredTypesMembersAndStableIds()
    {
        var result = await GetFileSkeletonTool.ExecuteAsync(
            _fixture.CreateServer(), ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);

        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        var file = Assert.Single(payload.GetProperty("files").EnumerateArray());
        var type = Assert.Single(file.GetProperty("types").EnumerateArray());
        Assert.Contains("T:", type.GetProperty("id").GetString(), StringComparison.Ordinal);
        var members = type.GetProperty("members").EnumerateArray().ToList();
        Assert.Contains(members, member => member.GetProperty("signature").GetString()!.Contains("Greet", StringComparison.Ordinal));
        Assert.All(members, member => Assert.Contains(":", member.GetProperty("id").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_TruncatesVisibleText()
    {
        var result = await GetFileSkeletonTool.ExecuteAsync(
            _fixture.CreateServer(), ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/Hierarchy.cs"], 512, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 512);
        Assert.Contains("maxResponseBytes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFinalResponseBudget_WithoutStructuredContent_ReturnsOriginalResponse()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = new string('x', 256) }]
        };

        var projected = GetFileSkeletonTool.ApplyFinalResponseBudget(result, 64);

        Assert.Same(result, projected);
    }

    [Fact]
    public void ApplyFinalResponseBudget_WithNonObjectStructuredContent_ReturnsOriginalResponse()
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = new string('x', 256) }],
            StructuredContent = JsonSerializer.SerializeToElement(new[] { "not-an-object" })
        };

        var projected = GetFileSkeletonTool.ApplyFinalResponseBudget(result, 64);

        Assert.Same(result, projected);
    }

    [Fact]
    public async Task ExecuteAsync_AbsolutePath_ResolvesSameAsRelativePath()
    {
        var state = _fixture.CreateServer();

        var relativeResult = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);
        var absoluteResult = await GetFileSkeletonTool.ExecuteAsync(
            state, [SymbolGraphMiniSolutionSpec.GreeterPath], CancellationToken.None);

        Assert.NotEqual(true, relativeResult.IsError);
        Assert.NotEqual(true, absoluteResult.IsError);
        var relativeText = Assert.IsType<TextContentBlock>(Assert.Single(relativeResult.Content)).Text;
        var absoluteText = Assert.IsType<TextContentBlock>(Assert.Single(absoluteResult.Content)).Text;
        Assert.Contains("Greet", relativeText, StringComparison.Ordinal);
        Assert.Contains("Greet", absoluteText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFile_ReturnsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, ["src/CompileErrorMini/BrokenClassA.cs"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFiles_ReturnsCombinedSkeletonsInSingleTurn()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state,
            filePaths: ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/Caller.cs"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
        Assert.Contains("Caller", text, StringComparison.Ordinal);
        Assert.Contains("---", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleFiles_WithOneNotFound_ContinuesAndIncludesWarning()
    {
        var state = _fixture.CreateServer();

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state,
            filePaths: ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/NonExistent.cs"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter", text, StringComparison.Ordinal);
        Assert.Contains("Datei nicht gefunden: `src/SymbolGraphMini/NonExistent.cs`", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SolutionDocumentPathResolver_ResolvesRelativePathWhenSolutionFilePathIsNull()
    {
        var ws = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var proj = ws.CurrentSolution.AddProject("AsmProj", "AsmProj", Microsoft.CodeAnalysis.LanguageNames.CSharp);
        var doc = proj.AddDocument("Greeter.cs", "public class Greeter {}", filePath: @"C:\Cache\SubDir\Greeter.cs");

        Assert.Null(doc.Project.Solution.FilePath);
        var resolved = AiNetLinter.Core.Documents.SolutionDocumentPathResolver.Find(doc.Project.Solution, "SubDir/Greeter.cs");

        Assert.NotNull(resolved);
        Assert.Equal("Greeter.cs", resolved.Name);
    }

    [Fact]
    public void SolutionDocumentPathResolver_FindsCommonDirectoryForNestedDocuments()
    {
        var paths = new[]
        {
            @"C:\Workspace\Decompiled\SubA\Foo.cs",
            @"C:\Workspace\Decompiled\SubB\Bar.cs",
        };
        var common = AiNetLinter.Core.Documents.SolutionDocumentPathResolver.FindCommonDirectory(paths);
        Assert.Equal(@"C:\Workspace\Decompiled", common);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIdsAreStructuredOnly_AndKindsRemainSelectable()
    {
        var result = await GetFileSkeletonTool.ExecuteAsync(
            _fixture.CreateServer(), ["src/SymbolGraphMini/Greeter.cs"], CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("id:", text, StringComparison.Ordinal);

        var type = result.StructuredContent!.Value.GetProperty("files")[0]
            .GetProperty("types")[0];
        Assert.StartsWith("s:", type.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("type", type.GetProperty("handoffKind").GetString());

        var members = type.GetProperty("members").EnumerateArray().ToList();
        Assert.NotEmpty(members);
        Assert.All(members, member =>
        {
            Assert.StartsWith("s:", member.GetProperty("id").GetString(), StringComparison.Ordinal);
            Assert.Equal("member", member.GetProperty("handoffKind").GetString());
        });
    }
}
