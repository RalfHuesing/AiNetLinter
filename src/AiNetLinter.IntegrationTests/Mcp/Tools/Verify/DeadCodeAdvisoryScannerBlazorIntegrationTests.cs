#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.Verify;

[Trait("Category", "Integration")]
public sealed class DeadCodeAdvisoryScannerBlazorIntegrationTests
{
    [Fact]
    public async Task ScanAsync_BlazorBindingsAreReferencesAndUnusedMemberRemainsCandidate()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var solution = catalog.Solution;
        var project = Assert.Single(solution.Projects);
        var siteView = Assert.IsAssignableFrom<INamedTypeSymbol>(
            (await project.GetCompilationAsync())?.GetTypeByMetadataName("BlazorPartialMini.SiteView"));
        var generatedDocuments = (await project.GetSourceGeneratedDocumentsAsync()).ToArray();

        Assert.NotEmpty(generatedDocuments);
        await AssertHasGeneratedReferenceAsync(siteView.GetMembers("HandleClick").Single(), solution, generatedDocuments);
        await AssertHasGeneratedReferenceAsync(siteView.GetMembers("Title").Single(), solution, generatedDocuments);
        await AssertHasGeneratedReferenceAsync(siteView.GetMembers("HandleConfirm").Single(), solution, generatedDocuments);

        var scopeFiles = GetScopeFiles(project);
        var scan = await DeadCodeAdvisoryScanner.ScanAsync(
            solution,
            new DeadCodeAdvisoryOptions(ScopeFiles: scopeFiles),
            CancellationToken.None);

        Assert.True(scan.Summary.DocumentsInScope > 0);
        Assert.DoesNotContain(scan.DeadSymbols, entry => entry.SymbolName is "HandleClick" or "Title" or "HandleConfirm");
        Assert.Contains(scan.DeadSymbols, entry => entry.SymbolName == "UnusedControlMember");

        var advisory = await VerifyAdvisoryProjector.CollectAsync(solution, scopeFiles, CancellationToken.None);
        Assert.Equal("complete", advisory.Completeness);
        Assert.True(advisory.DeadCode is { Status: "complete", Candidates: > 0 });
    }

    [Fact]
    public async Task ScanAsync_UnboundOnclickMarkupStringDoesNotProtectCodeBehindHandler()
    {
        using var fixture = new BlazorPartialMiniFixtureWorkspace();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BlazorPartialMini", "SiteView.razor"), """
            <h3>SiteView</h3>
            <button @onclick="UnboundHandler">Save</button>
            """);
        File.WriteAllText(fixture.SiteViewCsPath, """
            namespace BlazorPartialMini;

            public partial class SiteView
            {
                private void UnboundHandler() { }
            }
            """);

        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var solution = catalog.Solution;
        var project = Assert.Single(solution.Projects);
        var siteView = Assert.IsAssignableFrom<INamedTypeSymbol>(
            (await project.GetCompilationAsync())?.GetTypeByMetadataName("BlazorPartialMini.SiteView"));
        var generatedDocuments = (await project.GetSourceGeneratedDocumentsAsync()).ToArray();
        Assert.NotEmpty(generatedDocuments);

        var references = await SymbolFinder.FindReferencesAsync(siteView.GetMembers("UnboundHandler").Single(), solution);
        Assert.DoesNotContain(
            references.SelectMany(reference => reference.Locations),
            location => generatedDocuments.Any(document => document.Id == location.Document.Id));

        var scan = await DeadCodeAdvisoryScanner.ScanAsync(
            solution,
            new DeadCodeAdvisoryOptions(ScopeFiles: GetScopeFiles(project)),
            CancellationToken.None);

        Assert.True(scan.Summary.DocumentsInScope > 0);
        Assert.Contains(scan.DeadSymbols, entry => entry.SymbolName == "SiteView" && entry.Kind == "class");
        Assert.DoesNotContain(scan.DeadSymbols, entry => entry.SymbolName == "UnboundHandler");
    }

    private static async Task AssertHasGeneratedReferenceAsync(
        ISymbol symbol,
        Solution solution,
        Document[] generatedDocuments)
    {
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution);
        Assert.Contains(
            references.SelectMany(reference => reference.Locations),
            location => generatedDocuments.Any(document => document.Id == location.Document.Id));
    }

    private static System.Collections.Generic.IReadOnlySet<string> GetScopeFiles(Project project) => project.Documents
        .Where(document => document.FilePath is not null)
        .Select(document => Path.GetFullPath(document.FilePath!))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int GetLine(string path, string symbolName) =>
        Array.FindIndex(File.ReadAllLines(path), line => line.Contains(symbolName, StringComparison.Ordinal)) + 1;
}
