#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class FindReferencesToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public FindReferencesToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await FindReferencesTool.ExecuteAsync(state, "irrelevant", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_QualifiedName_ReturnsSingleMatch()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "Greeter.Greet", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_UnknownName_ReturnsSymbolNotFoundError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "DoesNotExistXyz", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
        // isError-Policy: SYMBOL_NOT_FOUND ist recoverable (naechster Schritt: find_symbol) —
        // IsError bleibt false, damit der Agent das Tool nicht aufgibt.
        Assert.NotEqual(true, error.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "DoesNotExistXyz", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, "Run", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("OtherCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_AmbiguousNameWithAssemblyIdentity_FormatsCurrentAssemblyIds()
    {
        using var context = new McpInMemoryTestContext();
        var identity = new AnalysisSymbolIdentity(new string('e', 64), 8);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution,
            "Run",
            CancellationToken.None,
            identity);

        Assert.Null(symbol);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content)).Text;
        Assert.Contains($"assembly:{identity.ContentHash}:{identity.Generation}:M:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id: `M:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionIdentifier_ReturnsSymbolAtPosition()
    {
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:5:19";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertySymbolNotAccessor()
    {
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:7:28";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Prefix", symbol!.Name);
        Assert.IsAssignableFrom<IPropertySymbol>(symbol);
        Assert.IsNotAssignableFrom<IMethodSymbol>(symbol);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionIdentifierWithSolutionRelativePath_ReturnsSymbolAtPosition()
    {
        var identifier = "src/SymbolGraphMini/Greeter.cs:5:19";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyOnMethodDeclaration_ReturnsMethodSymbol()
    {
        var identifier = "src/SymbolGraphMini/Greeter.cs:5";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
        Assert.IsAssignableFrom<IMethodSymbol>(symbol);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    [InlineData(5, 1000)]
    public async Task ResolveSymbolAsync_InvalidPosition_ReturnsRecoverableInvalidArgument(
        int line,
        int column)
    {
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:{line}:{column}";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution,
            identifier,
            CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        Assert.NotEqual(true, error!.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("WORKSPACE_DIAGNOSTIC", textContent.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task ResolveSymbolAsync_InvalidLineOnlyPosition_ReturnsRecoverableInvalidArgument(int line)
    {
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:{line}";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution,
            identifier,
            CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        Assert.NotEqual(true, error!.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_StableId_ReturnsSymbolAtId()
    {
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(resolved);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);
        Assert.NotNull(stableId);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, stableId!, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        // Sufficiency-Hinweis: nicht-trunkiertes Ergebnis ist vollstaendig, kein Read/Grep noetig.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedNameDepth1_StructuredContentDeserializesToCallSiteEntries()
    {
        // Nur der depth=1-Flachfall bekommt StructuredContent (siehe Kommentar in
        // FindReferencesTool.ExecuteAsync — depth>1 laesst CallGraphTraversal unveraendert).
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("callSites")
            .Deserialize<List<TransitiveCallSiteEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Contains(entries!, e => e.FilePath.Contains("Caller.cs", StringComparison.Ordinal));
        Assert.All(entries!, entry =>
        {
            Assert.Equal(1, entry.Depth);
            Assert.NotEmpty(entry.ReachedFromSymbolId);
        });
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_StructuredContentContainsCompleteness()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var completeness = result.StructuredContent!.Value.GetProperty("completeness");
        Assert.Equal(2, completeness.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(2, completeness.GetProperty("effectiveDepth").GetInt32());
        Assert.False(completeness.GetProperty("truncatedByMaxResults").GetBoolean());
        Assert.False(completeness.GetProperty("truncatedByNodeLimit").GetBoolean());
        var entries = result.StructuredContent.Value.GetProperty("callSites")
            .Deserialize<List<TransitiveCallSiteEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.NotEmpty(entries!);
        Assert.All(entries!, entry => Assert.InRange(entry.Depth, 1, 2));
    }

    [Fact]
    public async Task ExecuteAsync_StableId_ReturnsCallSiteInCaller()
    {
        var state = _fixture.CreateServer();
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await FindReferencesTool.ExecuteAsync(state, stableId!, maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 2, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
        // Ein trunkiertes Ergebnis bekommt NICHT den "vollstaendig"-Sufficiency-Hinweis —
        // die Meta-Zeile selbst signalisiert "weitere Calls noetig".
        Assert.DoesNotContain("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "ValidClassA.DoWork", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_StillReturnsCallSite()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_RealCallerChain_ReturnsBothLevels()
    {
        // Tool-Level-Kette A <- B <- C: find_references mit depth=2 muss Aufrufstellen auf
        // Ebene 1 UND 2 liefern — Ebene 2 mit ReachedFromSymbolId der Ebene-1-Methode.
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
        var (symbolB, _) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution, "ChainProbe.Runner.MethodB", CancellationToken.None);
        Assert.NotNull(symbolB);
        using var state = context.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state, "ChainProbe.Runner.MethodA", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("callSites")
            .Deserialize<List<TransitiveCallSiteEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        var level1 = entries!.Single(entry => entry.Depth == 1);
        Assert.Equal("Runner.MethodA", level1.SymbolName);
        Assert.Contains("Chain.cs", level1.FilePath, StringComparison.Ordinal);
        var level2 = entries!.Single(entry => entry.Depth == 2);
        Assert.Equal("Runner.MethodB", level2.SymbolName);
        Assert.Equal(DocumentationCommentId.CreateDeclarationId(symbolB!), level2.ReachedFromSymbolId);
    }

    [Fact]
    public async Task ExecuteAsync_Depth3_MultiProjectFixture_ReturnsStructuredEntriesWithOriginAndDepth()
    {
        using var context = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());

        var result = await FindReferencesTool.ExecuteAsync(
            context.CreateServer(), "Contracts.IProcessor.Execute", maxResults: 50, depth: 3, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ReferenceTraversalResult>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Completeness.EffectiveDepth);
        Assert.NotEmpty(payload.CallSites);
        Assert.Contains(payload.CallSites, entry => entry.Depth > 1);
        Assert.All(payload.CallSites, entry =>
        {
            Assert.InRange(entry.Depth, 1, 3);
            Assert.NotEmpty(entry.ReachedFromSymbolId);
        });
        Assert.Contains(payload.CallSites, entry => entry.ProjectName == "Application");
    }

    [Fact]
    public async Task ExecuteAsync_TransitiveMaxResults_ReportsOnlyMaxResultsTruncation()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 1, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var completeness = result.StructuredContent!.Value.GetProperty("completeness");
        Assert.True(completeness.GetProperty("truncatedByMaxResults").GetBoolean());
        Assert.False(completeness.GetProperty("truncatedByNodeLimit").GetBoolean());
        Assert.False(completeness.GetProperty("depthWasClamped").GetBoolean());
        Assert.Equal(1, completeness.GetProperty("shownCallSiteCount").GetInt32());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("1 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TransitiveStructuredContent_HasStableByteOrder()
    {
        var state = _fixture.CreateServer();

        var first = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 3, CancellationToken.None);
        var second = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 3, CancellationToken.None);

        Assert.NotNull(first.StructuredContent);
        Assert.NotNull(second.StructuredContent);
        Assert.Equal(
            first.StructuredContent!.Value.GetRawText(),
            second.StructuredContent!.Value.GetRawText());
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 100, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var completeness = result.StructuredContent!.Value.GetProperty("completeness");
        Assert.Equal(100, completeness.GetProperty("requestedDepth").GetInt32());
        Assert.Equal(3, completeness.GetProperty("effectiveDepth").GetInt32());
        Assert.True(completeness.GetProperty("depthWasClamped").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_Depth1_MatchesCurrentBehavior()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithSymbolAlias_ResolvesReferences()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest(null, MaxResults: 50, Depth: 1, Symbol: "Greeter.Greet"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }
}
