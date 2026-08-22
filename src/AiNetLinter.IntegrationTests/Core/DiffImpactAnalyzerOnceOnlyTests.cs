#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.IntegrationTests.Fixtures;
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

        Assert.Equal(1, counters.GitRuns);
        Assert.Equal(1, counters.TestSolutionScans);
        // LintRuns hat in dieser Stufe noch keine Inkrement-Stelle; der Nachweis folgt
        // mit der Violations-Stufe — hier wird nur das Nicht-Anwachsen gepinnt.
        Assert.Equal(0, counters.LintRuns);

        Assert.Equal(3, batch.DistinctTestFileCount);
    }
}
