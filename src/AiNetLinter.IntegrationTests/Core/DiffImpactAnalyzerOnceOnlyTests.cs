#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Core;

[Trait("Category", "Integration")]
public sealed class DiffImpactAnalyzerOnceOnlyTests
{
    [Fact]
    public async Task ChangeContextPipeline_InstrumentsGitAndTestSolutionExactlyOnce()
    {
        using var workspace = new ChangeContextMiniWorkspace();
        workspace.ChangeBothMethodBodiesWithoutCommitting();
        using var solutionOwner = workspace.CreateSolution();
        var symbols = await ChangeContextScenarioFactory.ResolveSymbolsAsync(solutionOwner.Solution);
        var counters = new DiffImpactCounters();

        // change-context-artiger Lauf: Git-Stufe (genau ein git diff) plus gebatchte
        // Test-Zuordnungsstufe (genau ein Solution-Durchlauf fuer BEIDE Ziel-Symbole).
        var analysis = await DiffImpactAnalyzer.RunAnalysisAsync(new DiffAnalysisRequest(
            solutionOwner.Solution,
            workspace.RootPath,
            GitSinceRef: null,
            Verbose: false,
            DiffSymbolScope.ChangeContext,
            Counters: counters));

        Assert.NotNull(analysis);
        Assert.Equal(2, analysis.ChangedSymbols.Count);
        Assert.Contains(analysis.ChangedSymbols, entry => entry.DisplayName == "OrderService.PlaceAsync");
        Assert.Contains(analysis.ChangedSymbols, entry => entry.DisplayName == "AuditLogger.LogInternal");

        var batch = await TestCoverageScanner.FindTestsForSymbolsCoreAsync(
            [symbols.PlaceAsync, symbols.LogInternal], solutionOwner.Solution, counters);

        // Violations-Stufe: dieselben Hunks/Zeiger-Symbole, derselbe Counters-Kanal —
        // genau ein solutionweiter Lint-Lauf auf der Workspace-Solution.
        var violationsStage = await DiffViolationScanner.CollectAsync(new DiffViolationScanRequest(
            solutionOwner.Solution,
            CreateAdHocLintConfig(),
            LinterConsole.Instance,
            workspace.RootPath,
            analysis.ChangedFiles,
            analysis.ChangedSymbols,
            counters));

        Assert.False(violationsStage.IsMalfunction);
        Assert.Equal(1, counters.GitRuns);
        Assert.Equal(1, counters.TestSolutionScans);
        Assert.Equal(1, counters.LintRuns);

        Assert.Equal(3, batch.DistinctTestFileCount);
    }

    private static Config CreateAdHocLintConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig()
    };
}
