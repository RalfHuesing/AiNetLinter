#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class FindSymbolScannerTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public FindSymbolScannerTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _fixture.Catalog.Solution, "Greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _fixture.Catalog.Solution, "Greet", kind: null, maxResults: 2);

        Assert.Contains("Treffer gesamt", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", result);
    }

    [Fact]
    public void TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine()
    {
        var fileList = new[] { "wwwroot/site.js", "wwwroot/Component.razor", "wwwroot/Page.xaml" };

        var result = McpTruncation.TruncateFileList(fileList, totalFiles: 3, maxFiles: 2);

        Assert.Contains("Dateien mit Textfund", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("search_pattern fuer Details", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNonCsHit_EmitsUntruncatedFileList()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _fixture.Catalog.Solution, "userService", kind: null, maxResults: 50);

        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
        Assert.Contains("Component.razor", result);
        Assert.Contains("Page.xaml", result);
        Assert.DoesNotContain("Dateien mit Textfund", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _fixture.Catalog.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            _fixture.Catalog.Solution, "Greeter", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'Greeter' (Kind-Filter: method)", result);
    }
}
