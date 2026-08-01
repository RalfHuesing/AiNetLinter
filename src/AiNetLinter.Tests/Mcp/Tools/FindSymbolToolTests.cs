using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class FindSymbolToolTests
{
    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(null);

        var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, 50, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Violating", kind: null, maxResults: 50);

        Assert.Contains("ViolatingClass.cs", result);
        Assert.Contains("Klasse", result);
        Assert.Contains(":", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Violating", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoMatch_ReturnsNoResultsText()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "DoesNotExistXyz", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyz'", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "userService", kind: null, maxResults: 50);

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
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        // Plain-NoMatch-Text (kein Miss-Hint-Pfad).
        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        // Explizit kein Miss-Hint: das Pattern kommt in keiner Nicht-C#-Datei vor.
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterMissHit_StillFires()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "userService", kind: "class", maxResults: 50);

        // Kind-Filter aendert nichts an der Non-C#-Suche — Miss-Hint feuert trotzdem.
        Assert.Contains("Kind-Filter: class", result);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_CaseInsensitive_MatchesRegardlessOfCase()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "violating", kind: null, maxResults: 50);

        Assert.Contains("ViolatingClass", result);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(catalog);

        var result = await FindSymbolTool.ExecuteAsync(state, "ValidClassA", kind: null, maxResults: 50, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        // EPIC-06 Aggregate-Warnhinweis: muss vor dem eigentlichen Treffer-Output erscheinen.
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
        Assert.Contains("ValidClassA", text, StringComparison.Ordinal);
    }
}
