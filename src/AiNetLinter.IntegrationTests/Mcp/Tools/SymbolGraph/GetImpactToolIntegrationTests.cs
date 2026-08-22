#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
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
    public async Task ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetImpactTool.ExecuteAsync(state, new GetImpactInput(null, "ValidClassA.DoWork", 50, 1), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
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
}
