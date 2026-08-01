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

        var result = await FindSymbolTool.ExecuteAsync(state, "irrelevant", null, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task FindMatchesAsync_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(catalog.Solution, "Violating", kind: null, CancellationToken.None);

        Assert.Contains("ViolatingClass.cs", result);
        Assert.Contains("Klasse", result);
        Assert.Contains(":", result);
    }

    [Fact]
    public async Task FindMatchesAsync_KindFilterExcludesNonMatchingKind()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(catalog.Solution, "Violating", kind: "method", CancellationToken.None);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAsync_NoMatch_ReturnsNoResultsText()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(catalog.Solution, "DoesNotExistXyz", kind: null, CancellationToken.None);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyz'", result);
    }

    [Fact]
    public async Task FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(
            catalog.Solution, "userService", kind: null, CancellationToken.None);

        // C#-Leermenge-Bestaetigung.
        Assert.Contains("Keine Treffer fuer 'userService'", result);
        // Miss-Hint-Markierung.
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        // Pfad-Liste enthaelt die Fixture-Datei.
        Assert.Contains("site.js", result);
        // Fallback-Verweis: search_pattern ist der naechste Schritt.
        Assert.Contains("search_pattern", result);
    }

    [Fact]
    public async Task FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(
            catalog.Solution, "DoesNotExistXyzBlub123", kind: null, CancellationToken.None);

        // Plain-NoMatch-Text (kein Miss-Hint-Pfad).
        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        // Explizit kein Miss-Hint: das Pattern kommt in keiner Nicht-C#-Datei vor.
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAsync_KindFilterMissHit_StillFires()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(
            catalog.Solution, "userService", kind: "class", CancellationToken.None);

        // Kind-Filter aendert nichts an der Non-C#-Suche — Miss-Hint feuert trotzdem.
        Assert.Contains("Kind-Filter: class", result);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
    }

    [Fact]
    public async Task FindMatchesAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(catalog.Solution, "violating", kind: null, CancellationToken.None);

        Assert.Contains("ViolatingClass", result);
    }
}
