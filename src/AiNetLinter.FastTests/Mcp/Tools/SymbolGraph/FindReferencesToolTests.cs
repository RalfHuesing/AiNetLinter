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
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(state, "ValidClassA.DoWork", maxResults: 50, depth: 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
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

    // --- Datei:Zeile-Fallback (ohne Spalte) ---

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifier_MatchesPositionIdentifierOnUniqueLine()
    {
        // OtherCaller.cs Zeile 3 ("public class OtherCaller") enthaelt nur ein einziges
        // quelltext-eigenes Symbol (die Klasse selbst) — Datei:Zeile muss dasselbe Symbol liefern
        // wie die explizite Datei:Zeile:Spalte-Angabe auf den Klassennamen. OtherCallerPath ist
        // ein absoluter Windows-Pfad mit Laufwerksbuchstabe (Fixture-Temp-Verzeichnis) — deckt
        // damit implizit auch die Laufwerksbuchstaben-Rekonstruktion aus TryParseLineOnlyPosition ab.
        var lineOnlyIdentifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:3";
        var positionIdentifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:3:14";

        var (lineOnlySymbol, lineOnlyError) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, lineOnlyIdentifier, CancellationToken.None);
        var (positionSymbol, positionError) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, positionIdentifier, CancellationToken.None);

        Assert.Null(lineOnlyError);
        Assert.Null(positionError);
        Assert.NotNull(lineOnlySymbol);
        Assert.True(SymbolEqualityComparer.Default.Equals(lineOnlySymbol, positionSymbol));
        Assert.Equal("OtherCaller", lineOnlySymbol!.Name);
    }

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifierWithMultipleSymbols_ReturnsAmbiguousSymbolError()
    {
        // Caller.cs Zeile 8 ("return greeter.Greet(\"World\");") traegt zwei eigenstaendige
        // quelltext-Symbole: die lokale Variable "greeter" und die Methode "Greet".
        var identifier = $"{SymbolGraphMiniSolutionSpec.CallerPath}:8";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("AMBIGUOUS_SYMBOL", textContent.Text);
        Assert.Contains("Caller.cs", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSymbolAsync_LineOnlyIdentifierWithNoSymbolsOnLine_ReturnsSymbolNotFoundError()
    {
        // Caller.cs Zeile 2 ist eine Leerzeile zwischen "namespace ...;" und der Klasse — kein
        // einziges Token mit aufloesbarem Symbol.
        var identifier = $"{SymbolGraphMiniSolutionSpec.CallerPath}:2";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ResolveSymbolAsync_WindowsDriveLetterPathWithLineOnly_ReconstructsFullPathAndReportsMissingFileNotSymbolNotFoundOnWrongPath()
    {
        // Regressionstest fuer die Format-Ambiguitaet aus dem Bug-Report: ein absoluter
        // Windows-Pfad mit Laufwerksbuchstabe erzeugt beim Split durch ':' drei Segmente
        // ("C", "\Datei.cs", "91"). Das Datei:Zeile-Fallback parst von hinten (letztes Segment =
        // Zeile) und rekonstruiert korrekt "C:\Datei.cs" als Pfad statt faelschlich nur "C" zu
        // verwenden. Diese konkrete Datei existiert nicht in der Test-Solution — das erwartete
        // SYMBOL_NOT_FOUND kommt also von der Datei-Suche, nicht von einem falsch geparsten Pfad
        // (siehe SymbolIdentifierResolverTests fuer den direkten Parsing-Nachweis).
        var identifier = "C:\\Datei.cs:91";

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(_fixture.Solution, identifier, CancellationToken.None);

        Assert.Null(symbol);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }


    [Fact]
    public async Task ExecuteAsync_GetTypeHierarchyTool_LineOnlyIdentifier_ResolvesTransitively()
    {
        // Belegt, dass der gemeinsame Resolver-Fix transitiv auch fuer get_type_hierarchy wirkt
        // (nicht nur fuer find_references direkt) — Greeter.cs Zeile 3 ist eine eindeutige
        // Klassendeklarationszeile.
        var state = _fixture.CreateServer();
        var identifier = $"{SymbolGraphMiniSolutionSpec.GreeterPath}:3";

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, identifier, GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        // Waere die Datei:Zeile-Aufloesung fehlgeschlagen oder auf ein Nicht-Typ-Symbol
        // gelaufen, kaeme SYMBOL_NOT_FOUND bzw. INVALID_ARGUMENT statt der Hierarchie-Ausgabe —
        // "Basisklassen:" ist nur im Erfolgsfall (Typ aufgeloest) im Text enthalten.
        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Basisklassen:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GetSymbolBodyTool_LineOnlyIdentifier_ResolvesTransitively()
    {
        // Belegt denselben transitiven Effekt fuer get_symbol_body — OtherCaller.cs Zeile 5
        // ("public string Run() => \"other\";") ist eine eindeutige Methodenzeile.
        var state = _fixture.CreateServer();
        var identifier = $"{SymbolGraphMiniSolutionSpec.OtherCallerPath}:5";

        var result = await GetSymbolBodyTool.ExecuteAsync(state, [identifier], GetSymbolBodyTool.DefaultMaxBodyLines, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Run", textContent.Text, StringComparison.Ordinal);
    }
}
