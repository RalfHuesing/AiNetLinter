using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Unit")]
public sealed class GetImpactToolTests
{
    private readonly McpInMemoryTestContext _fixture = new();

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "irrelevant", 50, 1), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BothGitRefAndSymbolGiven_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput("HEAD~1", "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierGivenDepth1_StructuredContentDeserializesToCallSiteEntries()
    {
        // Symbol-Branch depth=1 bekommt StructuredContent, analog find_references.
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("callSites")
            .Deserialize<List<CallSiteEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Contains(entries!, e => e.FilePath.Contains("Caller.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_StableSymbolIdentifierGiven_ReturnsCallSites()
    {
        var state = _fixture.CreateServer();
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, stableId, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbolIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "DoesNotExistXyz", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRepository_ReturnsEmptyResultNotCrash()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine betroffenen Aufrufstellen gefunden", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 2, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierWithDepth2_StillReturnsCallSite()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 50, 2), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }
}
