using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

/// <summary>
/// Dokumentiert den aktuellen Symbolgraph-Zustand vor der Razor-Generator-Integration: Die
/// generierte zweite Partial-Deklaration einer .razor-Komponente (mit dem impliziten
/// ": ComponentBase"-Basistyp) fliesst nicht in die von SourceFileCatalog.LoadAsync geladene
/// Compilation ein. Dadurch hat die .razor.cs-Codebehind-Klasse keinen Basistyp, und ihre
/// override-Lifecycle-Methoden matchen gegen keine virtuelle Methode — CS0115. Die Tests
/// dieser Klasse sind bewusst gruen: sie belegen den reproduzierbaren IST-Zustand, nicht ein
/// gewuenschtes Verhalten.
/// </summary>
public sealed class SourceFileCatalogBlazorPartialTests
{
    [Fact]
    public async Task LoadAsync_BlazorPartialFixture_ReportsCS0115OnOverrideLifecycleMethod()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var errorsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(catalog.Solution, CancellationToken.None);

        Assert.True(errorsByFile.TryGetValue(fixture.SiteViewCsPath, out var diagnostics));
        Assert.Contains(diagnostics!, d => d.Id == "CS0115");
    }

    [Fact]
    public async Task GetIndexScope_BlazorPartialFixture_ShowsAggregateCompileErrorHint()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.Matches(@"\b\d+\s+Datei(en)?\s+haben\s+Compile-Fehler", text);
    }

    [Fact]
    public async Task GetFileSkeleton_SiteViewRazorCs_MissesComponentBaseBaseType()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/BlazorPartialMini/SiteView.razor.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Diese Datei hat", text, System.StringComparison.Ordinal);
        Assert.Contains("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.Matches(@"CS\d{4}", text);
        Assert.DoesNotContain(": ComponentBase", text, System.StringComparison.Ordinal);
    }
}
