#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.FastTests.Fixtures;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

public sealed partial class FindReferencesToolTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyRazorCodeBehindResult_RecommendsMarkupBoundarySearch()
    {
        using var context = new McpInMemoryTestContext(McpInMemoryTestContext.CreateScenario(
            new ProjectSpec("RazorApp", [
                ("src/DialogPage.razor.cs", "namespace RazorApp; public sealed class DialogPage { public void OpenDialog() { } }"),
                ("src/DialogPage.razor", "<button @onclick=\"OpenDialog\">Open</button>")])));
        var state = context.CreateServer();

        var result = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest("RazorApp.DialogPage.OpenDialog", 50, 1, IncludeGenerated: false),
            CancellationToken.None);

        var text = TextOf(result);

        Assert.Null(result.IsError);
        Assert.Contains("Keine Aufrufstellen gefunden", text, StringComparison.Ordinal);
        Assert.Contains("razor_markup_not_indexed", text, StringComparison.Ordinal);
        Assert.Contains("OpenDialog", text, StringComparison.Ordinal);
        Assert.Contains("search_pattern", text, StringComparison.Ordinal);
        Assert.Contains("**/*.razor", text, StringComparison.Ordinal);
    }
}
