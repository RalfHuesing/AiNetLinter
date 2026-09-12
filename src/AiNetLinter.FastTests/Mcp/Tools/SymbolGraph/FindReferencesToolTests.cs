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
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed partial class FindReferencesToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public FindReferencesToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_ScopeFiltersBeforeLimit_AndRanksProductionFirst()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ScopeReferences.slnx",
            new ProjectSpec("App", [
                ("Service.cs", "namespace App; public class Service { public void Run() {} }"),
                ("ProductionCaller.cs", "namespace App; public class ProductionCaller { public void Call(Service service) => service.Run(); }")]),
            new ProjectSpec("App.Tests", [
                ("ServiceTests.cs", "namespace App.Tests; public class ServiceTests { public void Call(App.Service service) => service.Run(); }")],
                ["App"]));
        using var fixture = new McpInMemoryTestContext(solution);
        var state = fixture.CreateServer();

        var all = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest("App.Service.Run", 1, 1, McpScopeType.All, false),
            CancellationToken.None);
        var allText = TextOf(all);
        Assert.Contains("ProductionCaller.cs", allText, StringComparison.Ordinal);
        Assert.Contains("1 gezeigt", allText, StringComparison.Ordinal);

        var tests = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest("App.Service.Run", 1, 1, McpScopeType.Tests, false),
            CancellationToken.None);
        var testsText = TextOf(tests);
        Assert.Contains("ServiceTests.cs", testsText, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductionCaller.cs", testsText, StringComparison.Ordinal);
    }

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
    public async Task ResolveSymbolAsync_AmbiguousNameWithAssemblyIdentity_FormatsSelectableLocationsWithContentHandoffIds()
    {
        using var context = new McpInMemoryTestContext();
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\SymbolGraphMini.dll",
            new string('e', 64),
            generation: 8);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            context.Solution,
            "Run",
            CancellationToken.None,
            identity);

        Assert.Null(symbol);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content)).Text;
        Assert.Contains("handoffId: `a:", text, StringComparison.Ordinal);
        Assert.DoesNotContain(identity.ContentHash, text, StringComparison.Ordinal);
        Assert.DoesNotContain($":{identity.Generation}:M:", text, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedNameDepth1_RendersCallerAtFirstDepth()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_RendersCompletenessAndDepth()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Caller.cs", text, StringComparison.Ordinal);
        Assert.Contains("transitiver Aufrufer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATED", text, StringComparison.Ordinal);
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
        using var state = context.CreateServer();
        var result = await FindReferencesTool.ExecuteAsync(
            state, "ChainProbe.Runner.MethodA", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Chain.cs", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("Chain.cs", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task ExecuteAsync_Depth3_MultiProjectFixture_RendersOriginAndDepth()
    {
        using var context = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());

        var result = await FindReferencesTool.ExecuteAsync(
            context.CreateServer(), "Contracts.IProcessor.Execute", maxResults: 50, depth: 3, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Application", text, StringComparison.Ordinal);
        Assert.Contains("transitiver Aufrufer", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TransitiveMaxResults_ReportsOnlyMaxResultsTruncation()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 1, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("1 gezeigt", text, StringComparison.Ordinal);
        Assert.Contains("maxResults", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TransitiveContent_HasStableByteOrder()
    {
        var state = _fixture.CreateServer();

        var first = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 3, CancellationToken.None);
        var second = await FindReferencesTool.ExecuteAsync(
            state, "Greeter.Greet", maxResults: 50, depth: 3, CancellationToken.None);

        Assert.Equal(TextOf(first), TextOf(second));
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult()
    {
        var state = _fixture.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 100, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("depth auf 3 begrenzt", text, StringComparison.Ordinal);
        Assert.Contains("requestedDepth=100", text, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

}
