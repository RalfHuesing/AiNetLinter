#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

[Trait("Category", "Integration")]
public sealed class FindSymbolFileAdapterTests : IClassFixture<FindSymbolFileAdapterFixture>
{
    private readonly FindSymbolFileAdapterFixture fixture;

    public FindSymbolFileAdapterTests(FindSymbolFileAdapterFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchAndNonCsHit_EmitsUntruncatedFileList()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "userService", kind: null, maxResults: 50);

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
            fixture.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterExcludesNonMatchingKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'Greeter' (Kind-Filter: method)", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_ToolKindFilterExcludesNonMatchingKind()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: "method", maxResults: 50);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_GermanKindMethode_BehavesLikeEnglishMethod()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "Greeter", kind: "Methode", maxResults: 50);

        Assert.Contains("Keine Treffer", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoMatch_ReturnsNoResultsText()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "DoesNotExistXyz", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyz'", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "userService", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'userService'", result);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
        Assert.Contains("Component.razor", result);
        Assert.Contains("Page.xaml", result);
        Assert.Contains("search_pattern", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_ToolNoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "DoesNotExistXyzBlub123", kind: null, maxResults: 50);

        Assert.Contains("Keine Treffer fuer 'DoesNotExistXyzBlub123'", result);
        Assert.DoesNotContain("Hinweis: kein C#-Symbol", result);
    }

    [Fact]
    public async Task FindMatchesAndFormat_KindFilterMissHit_StillFires()
    {
        var result = await FindSymbolScanner.FindMatchesAndFormat(
            fixture.Solution, "userService", kind: "class", maxResults: 50);

        Assert.Contains("Kind-Filter: class", result);
        Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
        Assert.Contains("site.js", result);
    }
}

public sealed class FindSymbolFileAdapterFixture : IAsyncLifetime
{
    private IsolatedFixtureLease? lease;
    private SourceFileCatalog? catalog;

    public Microsoft.CodeAnalysis.Solution Solution => (catalog ?? throw new InvalidOperationException(
        $"{nameof(FindSymbolFileAdapterFixture)} wurde noch nicht initialisiert.")).Solution;

    public async ValueTask InitializeAsync()
    {
        lease = IsolatedFixtureLease.CopyFixture(FindSolutionRoot(), "SymbolGraphMini");
        catalog = await SourceFileCatalog.LoadAsync(lease.RootPath);
    }

    public ValueTask DisposeAsync()
    {
        catalog?.Dispose();
        lease?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
