using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class FindSymbolToolTests : IClassFixture<BaselineCatalogFixture>
{
    private readonly BaselineCatalogFixture _baselineFixture;
    private readonly SymbolGraphCatalogFixture _symbolGraphFixture;

    public FindSymbolToolTests(
        BaselineCatalogFixture baselineFixture,
        SymbolGraphCatalogFixture symbolGraphFixture)
    {
        _baselineFixture = baselineFixture;
        _symbolGraphFixture = symbolGraphFixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyNamePattern_ReturnsRecoverableInvalidArgument()
    {
        // isError-Policy: INVALID_ARGUMENT ist ein erwartbarer/recoverable Nutzerfehler, kein
        // Tool-Malfunction — IsError bleibt false, der Text traegt die Handlungsanleitung.
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_symbolGraphFixture.Catalog)));

        var result = await FindSymbolTool.ExecuteAsync(state, namePattern: "", kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Pattern angeben", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_KnownSymbol_StructuredContentDeserializesToSymbolLocationEntries()
    {
        // S1.3: StructuredContent ergaenzt den Text additiv — dieselbe Fundstelle wie die
        // Text-Zeile "Greeter.cs:... - Klasse: ...".
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_symbolGraphFixture.Catalog)));

        var result = await FindSymbolTool.ExecuteAsync(state, "Greeter", kind: "class", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var entries = result.StructuredContent!.Value.GetProperty("matches")
            .Deserialize<List<SymbolLocationEntry>>(McpJsonOptions.Default);
        Assert.NotNull(entries);
        Assert.Contains(entries!, e => e.FilePath.Contains("Greeter.cs", StringComparison.Ordinal) && e.Kind == "Klasse");
    }

    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _baselineFixture.Catalog.Solution, "Violating", kind: null, maxResults: 50);

        Assert.Contains("ViolatingClass.cs", result);
        Assert.Contains("Klasse", result);
        Assert.Contains(":", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _baselineFixture.Catalog.Solution, "Violating", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_GermanKindMethode_BehavesLikeEnglishMethod()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _baselineFixture.Catalog.Solution, "Violating", kind: "Methode", maxResults: 50);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_GermanKindKlasse_BehavesLikeEnglishClass()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _baselineFixture.Catalog.Solution, "Violating", kind: "Klasse", maxResults: 50);

        Assert.Contains("ViolatingClass.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownKind_ReturnsRecoverableInvalidArgument()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_symbolGraphFixture.Catalog)));

        var result = await FindSymbolTool.ExecuteAsync(state, namePattern: "Greeter", kind: "Enum", maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Enum", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoMatch_ReturnsNoResultsText()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _symbolGraphFixture.Catalog.Solution, "DoesNotExistXyz", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyz'", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _symbolGraphFixture.Catalog.Solution, "userService", kind: null, maxResults: 50);

        // C#-Leermenge-Bestaetigung.
        Assert.Contains("Keine Treffer fuer 'userService'", result);
        // Miss-Hint-Markierung.
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        // Pfad-Liste enthaelt die Fixture-Dateien (3 Stueck, untrunkiert).
        Assert.Contains("site.js", result);
        Assert.Contains("Component.razor", result);
        Assert.Contains("Page.xaml", result);
        // Fallback-Verweis: search_pattern ist der naechste Schritt.
        Assert.Contains("search_pattern", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _symbolGraphFixture.Catalog.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        // Plain-NoMatch-Text (kein Miss-Hint-Pfad).
        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        // Explizit kein Miss-Hint: das Pattern kommt in keiner Nicht-C#-Datei vor.
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterMissHit_StillFires()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _symbolGraphFixture.Catalog.Solution, "userService", kind: "class", maxResults: 50);

        // Kind-Filter aendert nichts an der Non-C#-Suche — Miss-Hint feuert trotzdem.
        Assert.Contains("Kind-Filter: class", result);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _baselineFixture.Catalog.Solution, "violating", kind: null, maxResults: 50);

        Assert.Contains("ViolatingClass", result);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await FindSymbolTool.ExecuteAsync(state, "ValidClassA", kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
        Assert.Contains("ValidClassA", text, StringComparison.Ordinal);
    }
}
