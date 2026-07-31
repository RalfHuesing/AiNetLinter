using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Collection("ConsoleTestCollection")]
public sealed class FindSymbolToolTests
{
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
        using var fixture = new BaselineMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var result = await FindSymbolTool.FindMatchesAsync(catalog.Solution, "DoesNotExistXyz", kind: null, CancellationToken.None);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyz'", result);
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
