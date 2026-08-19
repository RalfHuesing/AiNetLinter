#nullable enable

using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class FindSymbolScannerTests
{
    [Fact]
    public async Task FindMatchesAndFormat_SubstringMatch_ReturnsFileLineAndKind()
    {
        using var fixture = new McpInMemoryTestContext();

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: null, maxResults: 50);

        Assert.Contains("Greeter.cs", result);
        Assert.Contains("Klasse", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_TruncatesAtMaxResults_AppendsMetaLine()
    {
        using var fixture = new McpInMemoryTestContext();

        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greet", kind: null, maxResults: 2);

        Assert.Contains("Treffer gesamt", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("Pattern verfeinern oder maxResults erhöhen", result);
    }
}

[Trait("Category", "Unit")]
public sealed class FindSymbolTruncationTests
{
    [Fact]
    public void TruncateFileList_ExceedsMaxFiles_AppendsFileListMetaLine()
    {
        var fileList = new[] { "wwwroot/site.js", "wwwroot/Component.razor", "wwwroot/Page.xaml" };

        var result = McpTruncation.TruncateFileList(fileList, totalFiles: 3, maxFiles: 2);

        Assert.Contains("Dateien mit Textfund", result);
        Assert.Contains("2 gezeigt", result);
        Assert.Contains("search_pattern fuer Details", result);
    }
}
