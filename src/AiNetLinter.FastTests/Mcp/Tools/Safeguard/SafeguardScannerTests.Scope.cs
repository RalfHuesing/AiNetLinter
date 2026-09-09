#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.TestKit;
using AiNetLinter.FastTests.Fixtures;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Safeguard;

public sealed partial class SafeguardScannerTests
{
    [Fact]
    public async Task ComputeScoreAsync_ConfiguredGeneratedFileExclusion_DoesNotPoisonSolutionScore()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\SafeguardGeneratedFileTests.slnx",
            new ProjectSpec("InScope", [
                ("Generated.designer.cs", "namespace Generated; public sealed class GeneratedType { }"),
                ("Clean.cs", "namespace InScope; public sealed class Clean { public int Value() => 1; }") ]));

        var result = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            Solution: testSolution.Solution,
            Config: TestHelper.CreateDefaultConfig() with
            {
                FileFilters = new FileFiltersConfig
                {
                    ExcludeFilePatterns = ["*.designer.cs"],
                    ExcludeDirectoryPatterns = []
                }
            },
            Console: NullConsole.Instance,
            ScopeFilter: null,
            CancellationToken: CancellationToken.None));

        Assert.False(result.IsMalfunction);
        var score = Assert.IsType<ScoreResult>(result.Score);
        Assert.NotNull(score.Score);
        Assert.Equal("complete", score.Completeness);
        Assert.Equal("passed", score.Status);
        Assert.Equal(1, score.ExcludedDocumentCount);
        Assert.Contains("1 Dokumente bewusst ausgeschlossen", score.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComputeScoreAsync_ScopeFilter_FiltersViolationsAndClassMetricsTogether()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\SafeguardScopeTests.slnx",
            new ProjectSpec("InScope", [
                ("InScope.cs", "namespace InScope; public sealed class Clean { public int Value() => 1; }")]),
            new ProjectSpec("OutsideScope", [
                ("Outside.cs", "namespace OutsideScope; public class Dirty { public int Value() => 1; }")]));

        var result = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            Solution: testSolution.Solution,
            Config: CreateConfig(),
            Console: NullConsole.Instance,
            ScopeFilter: "InScope",
            CancellationToken: CancellationToken.None,
            MinScoreThreshold: 8.0));

        Assert.False(result.IsMalfunction);
        Assert.NotNull(result.Score);
        var score = result.Score!;
        Assert.NotNull(score.Score);
        Assert.Equal("InScope", score.Scope);
        Assert.True(score.ScoreIsNotScope);
        Assert.Equal("complete", score.Completeness);
        Assert.Contains("Quality-Gate", score.StatusCause, StringComparison.Ordinal);
        Assert.DoesNotContain(score.Violations, v => v.FilePath.Contains("Outside.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("1 Klassen analysiert", score.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComputeScoreAsync_ScopeFilterWithoutMatchingDocument_IsNotDecidable()
    {
        using var testSolution = CreateSolution(("Only.cs", "namespace Only; public sealed class Clean { }"));

        var result = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            Solution: testSolution.Solution,
            Config: CreateConfig(),
            Console: NullConsole.Instance,
            ScopeFilter: "DoesNotExistAnywhere",
            CancellationToken: CancellationToken.None));

        Assert.NotNull(result.Score);
        var score = result.Score!;
        Assert.Null(score.Score);
        Assert.Null(score.Passed);
        Assert.Equal("not_decidable", score.Status);
        Assert.DoesNotContain("PASS", score.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("0.00/10", score.Summary, StringComparison.Ordinal);
    }
}
