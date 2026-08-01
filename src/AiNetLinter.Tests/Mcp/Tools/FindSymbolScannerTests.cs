#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class FindSymbolScannerTests
{
    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        // "Greet" matcht in der Fixture in 7 Symbolen: IGreeting (Klasse), BaseGreeting,
        // SpecialGreeting, DisposableGreeting, Greeter (alle Klassen) + 3x Greet-Methode
        // (IGreeting.Greet, BaseGreeting.Greet, Greeter.Greet). maxResults = 2 erzwingt
        // Trunkierung.
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greet", kind: null, maxResults: 2);

        // Meta-Zeile der Haupt-Treffer-Trunkierung.
        Assert.Contains("Treffer gesamt", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", result);
    }

    [Fact]
    public void TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine()
    {
        // Direkt-Test des TruncateFileList-Helpers: 3 Dateien, maxFiles = 2 → Meta-Zeile.
        var fileList = new[] { "wwwroot/site.js", "wwwroot/Component.razor", "wwwroot/Page.xaml" };

        var result = McpTruncation.TruncateFileList(fileList, totalFiles: 3, maxFiles: 2);

        Assert.Contains("Dateien mit Textfund", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("search_pattern fuer Details", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNonCsHit_EmitsUntruncatedFileList()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        // userService matcht in 3 Nicht-C#-Dateien (site.js, Component.razor, Page.xaml).
        // Bei maxFiles = 10 (Default) wird NICHT trunkiert (3 ≤ 10) — alle 3 Pfade erscheinen,
        // KEINE Meta-Zeile. Trunkierung wird separat in TruncateFileList_ExceedsMaxFiles_
        // AppendsFileListMetaLine getestet.
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "userService", kind: null, maxResults: 50);

        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
        Assert.Contains("Component.razor", result);
        Assert.Contains("Page.xaml", result);
        Assert.DoesNotContain("Dateien mit Textfund", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            catalog.Solution, "Greeter", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'Greeter' (Kind-Filter: method)", result);
    }
}
