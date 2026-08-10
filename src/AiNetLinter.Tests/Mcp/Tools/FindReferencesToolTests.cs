using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class FindReferencesToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public FindReferencesToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
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
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_UnknownName_ReturnsSymbolNotFoundError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "DoesNotExistXyz", CancellationToken.None);

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
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "DoesNotExistXyz", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_AmbiguousSimpleName_ReturnsAmbiguousSymbolError()
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, "Run", CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("OtherCaller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionIdentifier_ReturnsSymbolAtPosition()
    {
        var identifier = $"{_fixture.Workspace.GreeterPath}:5:19";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertySymbolNotAccessor()
    {
        var identifier = $"{_fixture.Workspace.GreeterPath}:7:28";
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, identifier, CancellationToken.None);

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
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, identifier, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_StableId_ReturnsSymbolAtId()
    {
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        Assert.NotNull(resolved);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);
        Assert.NotNull(stableId);

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Catalog.Solution, stableId!, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(symbol);
        Assert.Equal("Greet", symbol!.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        // Q5 Sufficiency-Hinweis: nicht-trunkiertes Ergebnis ist vollstaendig, kein Read/Grep noetig.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQualifiedNameDepth1_StructuredContentDeserializesToCallSiteEntries()
    {
        // S1.3: nur der depth=1-Flachfall bekommt StructuredContent (siehe Kommentar in
        // FindReferencesTool.ExecuteAsync — depth>1 laesst CallGraphTraversal unveraendert).
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = JsonSerializer.Deserialize<List<CallSiteEntry>>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Contains(entries!, e => e.FilePath.Contains("Caller.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_StructuredContentIsNull()
    {
        // Bewusste Entscheidung (siehe FindReferencesTool.ExecuteAsync): depth>1 traversiert ueber
        // CallGraphTraversal, das intern reine Strings statt eines strukturierten Zwischenmodells
        // baut — kein StructuredContent fuer diesen Fall.
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Null(result.StructuredContent);
    }

    [Fact]
    public async Task ExecuteAsync_StableId_ReturnsCallSiteInCaller()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter.Greet", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await FindReferencesTool.ExecuteAsync(state, stableId!, maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSymbolWithManyCallSites_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 2, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Treffer gesamt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("2 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", textContent.Text, StringComparison.Ordinal);
        // Q5: ein trunkiertes Ergebnis bekommt NICHT den "vollstaendig"-Sufficiency-Hinweis —
        // die Meta-Zeile selbst signalisiert "weitere Calls noetig".
        Assert.DoesNotContain("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "ValidClassA.DoWork", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Depth2_StillReturnsCallSite()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 2, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 100, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_Depth1_MatchesCurrentBehavior()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await FindReferencesTool.ExecuteAsync(state, "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Caller.cs", textContent.Text, System.StringComparison.Ordinal);
    }
}
