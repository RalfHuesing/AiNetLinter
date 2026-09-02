#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Integration")]
public sealed class GetImpactToolIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_GitRefUncommittedChange_StructuredContentDeserializesToCallSiteEntries()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.True(result.StructuredContent!.Value.TryGetProperty("callSites", out var entries));
        Assert.Contains("CalculatorCaller.cs", entries.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeDiffAsync_OnModifiedWorkspace_MatchesEntriesWrapperAndCarriesStructuredData()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));
        var solution = state.GetCurrentSolution()!;

        var analysis = await DiffImpactAnalyzer.AnalyzeDiffAsync(solution, fixture.RootPath, gitSinceRef: null, verbose: false);
        var wrapperEntries = await DiffImpactAnalyzer.AnalyzeEntriesAsync(solution, fixture.RootPath, gitSinceRef: null, verbose: false);

        Assert.NotNull(analysis);
        Assert.Equal(Path.GetFullPath(fixture.RootPath), Path.GetFullPath(analysis.RepositoryRoot));
        Assert.Null(analysis.SinceRef);

        var changedFile = Assert.Single(analysis.ChangedFiles, file => file.FilePath.EndsWith("Calculator.cs", StringComparison.Ordinal));
        Assert.NotEmpty(changedFile.Ranges);
        Assert.All(changedFile.Ranges, range =>
        {
            Assert.True(range.StartLine >= 1);
            Assert.True(range.LineCount >= 1);
        });

        Assert.Contains(
            analysis.ChangedSymbols,
            entry => entry.SymbolId == "M:GitImpactMini.Calculator.Add(System.Int32,System.Int32)~System.Int32"
                     && entry.Accessibility == Accessibility.Public
                     && entry.Kind == "Method"
                     && entry.FilePath.Contains("Calculator.cs", StringComparison.Ordinal));

        Assert.NotEmpty(analysis.References.CallSites);
        Assert.Equal(analysis.References.CallSites.Count, analysis.References.Completeness.TotalCallSiteCount);
        Assert.False(analysis.References.Completeness.TruncatedByMaxResults);
        Assert.False(analysis.References.Completeness.TruncatedByNodeLimit);

        // Wrapper-Aequivalenz Ende-zu-Ende: References.CallSites element- und reihenfolgetreu
        // zur AnalyzeEntriesAsync-Ausgabe auf derselben Solution.
        Assert.Equal(
            wrapperEntries.Select(entry => (entry.FilePath, entry.Line, entry.SymbolName, entry.ProjectName)),
            analysis.References.CallSites.Select(callSite => (callSite.FilePath, callSite.Line, callSite.SymbolName, callSite.ProjectName)));
    }

    [Fact]
    public async Task AnalyzeChangeContextAsync_OnModifiedPrivateMethod_ListsSymbolWithoutCallSites_AndCallersWrapperOmitsIt()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorNormalizeBodyWithoutCommitting();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));
        var solution = state.GetCurrentSolution()!;

        var analysis = await DiffImpactAnalyzer.AnalyzeChangeContextAsync(solution, fixture.RootPath, gitSinceRef: null, verbose: false);
        var callersEntries = await DiffImpactAnalyzer.AnalyzeEntriesAsync(solution, fixture.RootPath, gitSinceRef: null, verbose: false);

        Assert.NotNull(analysis);
        // Die private Methode erscheint im breiten Scope auch ohne jegliche Call-Sites.
        var privateEntry = Assert.Single(analysis.ChangedSymbols);
        Assert.Equal("M:GitImpactMini.Calculator.Normalize(System.Int32)~System.Int32", privateEntry.SymbolId);
        Assert.Equal(Accessibility.Private, privateEntry.Accessibility);
        Assert.Equal("Method", privateEntry.Kind);
        Assert.Empty(analysis.References.CallSites);
        Assert.Equal(1, analysis.References.Completeness.VisitedNodeCount);
        Assert.Equal(analysis.References.CallSites.Count, analysis.References.Completeness.TotalCallSiteCount);
        Assert.False(analysis.References.Completeness.TruncatedByMaxResults);
        Assert.False(analysis.References.Completeness.TruncatedByNodeLimit);
        // Der schmale callers-Pfad auf derselben Workspace enthaelt sie nicht.
        Assert.Empty(callersEntries);
    }

    [Fact]
    public async Task ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvableGitRef_ReturnsRecoverableAnalysisFailedNotEmptyResult()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput("does-not-exist-xyz", null, 50, 1), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("ANALYSIS_FAILED", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GitRefUncommittedWithManyCallSites_TruncatesAtMaxResults()
    {
        using var fixture = new GitImpactMiniFixtureWorkspace();
        fixture.ChangeCalculatorAddBodyWithoutCommitting();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, null, 2, 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("2 gezeigt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "ValidClassA.DoWork", 50, 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ChangeContext_ReturnsFullContractOnMiniWorkspace()
    {
        using var workspace = new ChangeContextMiniWorkspace();
        workspace.ChangeBothMethodBodiesWithoutCommitting();
        using var solutionOwner = workspace.CreateSolution();
        using var state = CreateChangeContextServer(solutionOwner);

        var result = await GetImpactTool.ExecuteAsync(
            state, new GetImpactInput(null, null, 50, 1, DetailLevel: "change-context"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = result.StructuredContent!.Value;
        var raw = structured.GetRawText();

        // Beide geaenderten Methoden inkl. der privaten LogInternal erscheinen als changedSymbols.
        Assert.Equal(2, structured.GetProperty("changedSymbols").GetArrayLength());
        Assert.Contains("OrderService.PlaceAsync", raw, StringComparison.Ordinal);
        Assert.Contains("AuditLogger.LogInternal", raw, StringComparison.Ordinal);
        var privateEntry = structured.GetProperty("changedSymbols").EnumerateArray()
            .Single(symbol => symbol.GetProperty("documentationCommentId").GetString()!.Contains("LogInternal", StringComparison.Ordinal));
        Assert.Equal("Private", privateEntry.GetProperty("accessibility").GetString());

        // Call-Sites fuer PlaceAsync (Invocation im Testprojekt).
        var callSites = structured.GetProperty("callSites");
        Assert.True(callSites.GetArrayLength() > 0);
        Assert.Contains(callSites.EnumerateArray(), callSite => callSite.GetProperty("symbolName").GetString() == "OrderService.PlaceAsync");

        // Nicht-leere statische Test-Zuordnung plus empfohlene dotnet test-Befehle.
        Assert.NotEmpty(structured.GetProperty("testAssociations").EnumerateArray());
        var commands = structured.GetProperty("recommendedTestCommands");
        Assert.True(commands.GetArrayLength() > 0);
        Assert.All(commands.EnumerateArray(), command => Assert.StartsWith("dotnet test ", command.GetString()!, StringComparison.Ordinal));

        // Vollstaendigkeit: nichts trunkiert bei zwei Symbolen gegen den Default-Cap.
        var completeness = structured.GetProperty("completeness");
        Assert.Equal(2, completeness.GetProperty("changedSymbolsTotal").GetInt32());
        Assert.Equal(2, completeness.GetProperty("changedSymbolsShown").GetInt32());
        Assert.False(completeness.GetProperty("symbolsTruncated").GetBoolean());
        Assert.False(completeness.GetProperty("testsTruncated").GetBoolean());

        // Violations sind strikt diffbezogen: nur aus Hunks oder Spannen GEZEIGTER Symbole.
        AssertViolationsWithinHunksOrShownSpans(structured, workspace.RootPath);

        // Textform: Counts-Zeile plus Sufficiency-Hint (vollstaendiges Ergebnis).
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Change-Context:", text, StringComparison.Ordinal);
        Assert.Contains("[HINWEIS]: Diese Daten sind vollstaendig", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ChangeContextWithCap_ShowsDeterministicSubsetWithMetadata()
    {
        using var workspace = new ChangeContextMiniWorkspace();
        workspace.ChangeBothMethodBodiesWithoutCommitting();
        using var solutionOwner = workspace.CreateSolution();
        using var state = CreateChangeContextServer(solutionOwner);

        var result = await GetImpactTool.ExecuteAsync(
            state,
            new GetImpactInput(null, null, 50, 1, DetailLevel: "change-context", MaxChangedSymbols: 1),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = result.StructuredContent!.Value;
        var raw = structured.GetRawText();

        var completeness = structured.GetProperty("completeness");
        Assert.Equal(2, completeness.GetProperty("changedSymbolsTotal").GetInt32());
        Assert.Equal(1, completeness.GetProperty("changedSymbolsShown").GetInt32());
        Assert.True(completeness.GetProperty("symbolsTruncated").GetBoolean());

        // Deterministische Kappung nach Projekt → Datei → Startzeile → Symbol-ID:
        // App.OrderService.PlaceAsync (Projekt "App") zeigt, App.Core.AuditLogger.LogInternal faellt weg.
        var shown = Assert.Single(structured.GetProperty("changedSymbols").EnumerateArray().ToArray());
        Assert.Contains("PlaceAsync", shown.GetProperty("documentationCommentId").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("LogInternal", raw, StringComparison.Ordinal);

        // Der weggekappte Symbol-Eintrag taucht NIRGENDS auf — auch nicht in callSites/
        // testAssociations/violations (der komplette Raw-Text enthaelt ihn bereits nicht).

        // Textform: Meta-Zeile statt Sufficiency-Hint.
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("[HINWEIS]: Diese Daten sind vollstaendig", text, StringComparison.Ordinal);
        Assert.Contains("[Teilergebnis:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GitImpactMiniFixtureWorkspace_DisposeTwice_DeletesRootWithoutThrowing()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        var rootPath = workspace.RootPath;

        workspace.Dispose();
        var exception = Record.Exception(() => workspace.Dispose());

        Assert.Null(exception);
        Assert.False(Directory.Exists(rootPath));
    }

    private static McpCodeGraphServer CreateChangeContextServer(RoslynTestSolution solutionOwner) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            // Deterministische Violation-Regel fuer die diffbezogene Filterung (Klassendeklarationen).
            Config: new Config
            {
                Global = new GlobalConfig { EnforceSealedClasses = true },
                Metrics = new MetricsConfig()
            },
            ReadOnlySolutionSnapshot: solutionOwner.Solution)));

    private static void AssertViolationsWithinHunksOrShownSpans(JsonElement structured, string rootPath)
    {
        foreach (var violation in structured.GetProperty("violations").EnumerateArray())
        {
            var violationPath = Path.GetFullPath(violation.GetProperty("filePath").GetString()!);
            var line = violation.GetProperty("lineNumber").GetInt32();
            var inHunks = structured.GetProperty("changedFiles").EnumerateArray().Any(file =>
                SameWorkspaceFile(rootPath, file.GetProperty("filePath").GetString()!, violationPath)
                && file.GetProperty("ranges").EnumerateArray().Any(range =>
                    range.GetProperty("startLine").GetInt32() <= line
                    && line < range.GetProperty("startLine").GetInt32()
                    + range.GetProperty("lineCount").GetInt32()));
            var inSpan = structured.GetProperty("changedSymbols").EnumerateArray().Any(symbol =>
                SameWorkspaceFile(rootPath, symbol.GetProperty("filePath").GetString()!, violationPath)
                && symbol.GetProperty("startLine").GetInt32() <= line
                && line <= symbol.GetProperty("endLine").GetInt32());
            Assert.True(inHunks || inSpan, $"Violation ausserhalb von Hunk/Spanne: {violationPath}:{line}");
        }
    }

    private static bool SameWorkspaceFile(string rootPath, string relativePath, string absolutePath) =>
        string.Equals(
            Path.GetFullPath(Path.Combine(rootPath, relativePath)),
            absolutePath,
            StringComparison.OrdinalIgnoreCase);
}
