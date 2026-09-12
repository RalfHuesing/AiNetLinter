using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using Microsoft.CodeAnalysis;
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
    public async Task ExecuteAsync_SymbolIdentifierGivenDepth1_RendersCallSiteEntries()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifier_EnrichesResponseWithAffectedProjectsAndTests()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Auswirkungsanalyse (Impact)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Betroffene Projekte", textContent.Text, StringComparison.Ordinal);

        Assert.Contains("Test", textContent.Text, StringComparison.Ordinal);
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
        Assert.Contains("kein Git-Repository", textContent.Text, StringComparison.Ordinal);
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
        Assert.Contains("transitiver Aufrufer", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifier_Depth2RealCallerChain_ReturnsBothLevels()
    {
        // Symbol-Branch auf echter Kette A <- B <- C: Ebene 1 (Aufruf in B) und Ebene 2
        // (Aufruf in C) im Content mit korrekter Herkunft je Ebene.
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("ChainProbe", [
                ("Chain.cs", """
                    namespace ChainProbe;

                    public class Runner
                    {
                        public void MethodA() { }
                        public void MethodB() { MethodA(); }
                        public void MethodC() { MethodB(); }
                    }
                    """)
            ])));
        var state = context.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "ChainProbe.Runner.MethodA", 50, 2), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Chain.cs", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("Chain.cs", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierCompleteResult_AppendsSufficiencyHint()
    {
        // Ein vollstaendiges Ergebnis benoetigt keinen redundanten Text-Hinweis.
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("[HINWEIS]: Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SymbolIdentifierTruncatedResult_OmitsSufficiencyHint()
    {
        // Gegenstueck: ein trunkiertes Ergebnis traegt seine Trunkierungs-Meta-Zeile und
        // gerade NICHT den Vollstaendigkeits-Hinweis (die beiden schliessen sich aus).
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 2, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("[HINWEIS]: Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ChangeContextWithSymbolIdentifier_ReturnsRecoverableInvalidArgumentWithFeatureContextHint()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, "Greeter.Greet", 50, 1, DetailLevel: "change-context"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("get_feature_context", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownDetailLevel_ReturnsRecoverableInvalidArgumentWithAllowedValues()
    {
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, null, 50, 1, DetailLevel: "deep-dive"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("callers", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("change-context", textContent.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("callers")]
    [InlineData("CALLERS")]
    public async Task ExecuteAsync_DetailLevelCallersVariants_SelectDefaultGitBranch(string? detailLevel)
    {
        // null/leer/"callers" (auch gross/klein) waehlt den Git-Branch ohne Detailfilter.
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, null, 50, 1, DetailLevel: detailLevel), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("kein Git-Repository", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ChangeContextWithoutRepository_RendersEmptyContract()
    {
        // Kein Repo / leerer Diff ist KEIN Fehlerfall: der Content macht den leeren Kontext sichtbar.
        var state = _fixture.CreateServer();

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, null, 50, 1, DetailLevel: "Change-Context"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("[HINWEIS]: Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);

        Assert.Contains("kein Git-Repository", textContent.Text, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
