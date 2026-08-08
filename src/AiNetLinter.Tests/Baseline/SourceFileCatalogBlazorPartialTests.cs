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
/// Belegt, dass die vom Razor-Source-Generator erzeugte zweite Partial-Deklaration einer
/// .razor-Komponente (mit dem impliziten ": ComponentBase"-Basistyp) korrekt in die von
/// SourceFileCatalog.LoadAsync geladene Compilation einfliesst. Die .razor.cs-Codebehind-Klasse
/// hat dadurch einen Basistyp, und ihre override-Lifecycle-Methoden matchen gegen die
/// entsprechenden virtuellen Methoden von ComponentBase — kein CS0115. get_file_skeleton zeigt
/// den Basistyp jetzt ebenfalls an, semantisch aufgeloest ueber das gemergte Partial-Symbol, mit
/// einem Hinweis, dass er aus einer anderen Partial-Deklaration stammt.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SourceFileCatalogBlazorPartialTests
{
    [Fact]
    public async Task LoadAsync_BlazorPartialFixture_ResolvesComponentBaseWithoutCompileErrors()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

        var errorsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(catalog.Solution, CancellationToken.None);
        Assert.False(errorsByFile.TryGetValue(fixture.SiteViewCsPath, out _));

        var project = catalog.Solution.Projects.Single();
        var compilation = await project.GetCompilationAsync();
        var siteView = compilation!.GetTypeByMetadataName("BlazorPartialMini.SiteView");
        Assert.Equal("Microsoft.AspNetCore.Components.ComponentBase", siteView?.BaseType?.ToDisplayString());
    }

    [Fact]
    public async Task GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Hinweis:", text, System.StringComparison.Ordinal);
        Assert.Contains(".cs: 1 Datei (voll vom Symbolgraph abgedeckt)", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseAndNoCompileError()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetFileSkeletonTool.ExecuteAsync(
            state, "src/BlazorPartialMini/SiteView.razor.cs", CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("CS0115", text, System.StringComparison.Ordinal);
        Assert.Contains("ComponentBase", text, System.StringComparison.Ordinal);
    }
}
