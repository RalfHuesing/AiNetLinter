#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Models;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

/// <summary>
/// Tests der internen diff-bezogenen Violations-Stufe: die pure Filterfunktion auf synthetischen
/// Eingaben (kein Lint, kein Git — inklusive Pfadsemantik: Hunks repo-root-relativ, Symbole
/// solution-relativ, Violations absolut) und die Stufe selbst mit echter
/// <see cref="AiNetLinter.Core.LinterEngine"/> auf einer In-Memory-Solution mit Ad-hoc-Config.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DiffViolationFilterTests
{
    // Realistische Verschachtelung: Solution-Verzeichnis liegt UNTERHALB der Repo-Wurzel —
    // nur so kann eine Datei gleichzeitig Hunk- (repo-relativ) und Spannen-Treffer (solution-relativ) sein.
    private static readonly DiffPathContext Paths = new(@"C:\repo", @"C:\repo\src\sol");

    /// <summary>Klassendeklarationszeile von OrderService in der Szenario-Quelldatei (namespace, Leerzeile, class).</summary>
    private const int ScenarioOrderClassLine = 3;

    private static RuleViolation Vio(string absolutePath, int line, string rule) => new()
    {
        FilePath = absolutePath,
        LineNumber = line,
        RuleName = rule,
        Details = "details",
        Guidance = "guidance"
    };

    private static ChangedFileRange Hunks(string repoRelativePath, params HunkRange[] ranges) =>
        new(repoRelativePath, ranges);

    private static ChangedSymbolEntry SymbolEntry(string solutionRelativePath, int startLine, int endLine) =>
        new("S:Id", "Type.Member", "Method", Accessibility.Public, "P", solutionRelativePath, startLine, endLine);

    private static IReadOnlyList<RuleViolation> RunFilter(
        IReadOnlyList<RuleViolation> violations,
        IReadOnlyList<ChangedFileRange> changedFiles,
        IReadOnlyList<ChangedSymbolEntry> shownSymbols) =>
        DiffViolationScanner.FilterDiffRelevantViolations(violations, changedFiles, shownSymbols, Paths);

    private static Config CreateScenarioLintConfig() => new()
    {
        Global = new GlobalConfig { EnforceSealedClasses = true },
        Metrics = new MetricsConfig()
    };

    private static string ScenarioSolutionDirectory() =>
        Path.GetDirectoryName(Path.GetFullPath(ChangeContextScenarioFactory.VirtualSolutionFilePath))!;

    [Fact]
    public void Filter_ViolationInsideHunkIncluded_SameFileNeighborOutsideExcluded()
    {
        var changedFiles = new[] { Hunks(@"src\sol\Second.cs", new HunkRange(10, 3)) };
        var violations = new[]
        {
            Vio(@"C:\repo\src\sol\Second.cs", 9, "R1"),
            Vio(@"C:\repo\src\sol\Second.cs", 10, "R1"),
            Vio(@"C:\repo\src\sol\Second.cs", 12, "R2"),
            Vio(@"C:\repo\src\sol\Second.cs", 13, "R2")
        };

        var result = RunFilter(violations, changedFiles, []);

        Assert.Equal(new[] { 10, 12 }, result.Select(v => v.LineNumber));
    }

    [Fact]
    public void Filter_ViolationOnlyInShownSymbolSpan_Included_WithoutAnyHunk()
    {
        var shownSymbols = new[] { SymbolEntry(@"App\OrderService.cs", 20, 30) };
        var violations = new[]
        {
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 19, "R"),
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 20, "R"),
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 25, "R"),
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 30, "R"),
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 31, "R")
        };

        var result = RunFilter(violations, [], shownSymbols);

        Assert.Equal(new[] { 20, 25, 30 }, result.Select(v => v.LineNumber));
    }

    [Fact]
    public void Filter_HunkWithLineCountZero_NeverMatches_EvenOnStartLine()
    {
        var changedFiles = new[] { Hunks(@"f.cs", new HunkRange(5, 0)) };
        var violations = new[] { Vio(@"C:\repo\f.cs", 5, "R"), Vio(@"C:\repo\f.cs", 6, "R") };

        var result = RunFilter(violations, changedFiles, []);

        Assert.Empty(result);
    }

    [Fact]
    public void Filter_NormalizesRepoRelativeSolutionRelativeAndAbsolutePaths_TolerantToSeparatorAndCase()
    {
        var changedFiles = new[] { Hunks("Src/App/OrderService.cs", new HunkRange(3, 1)) };
        var shownSymbols = new[] { SymbolEntry("app/other.cs", 7, 9) };
        var violations = new[]
        {
            Vio(@"C:\REPO\SRC\APP\orderservice.cs", 3, "R"),
            Vio(@"C:\REPO\src\sol\APP\Other.CS", 8, "R"),
            Vio(@"C:\repo\src\sol\App\OrderService.cs", 3, "R")
        };

        var result = RunFilter(violations, changedFiles, shownSymbols);

        // Nur die beiden echten Treffer; die gleichnamige Datei unter dem Solution-Baum hat keinen Hunk.
        Assert.Equal(new[] { 3, 8 }, result.Select(v => v.LineNumber));
    }

    [Fact]
    public void Filter_DualConditionYieldsExactlyOneEntry_AndSortsFileThenLineThenRuleCaseInsensitive()
    {
        var changedFiles = new[] { Hunks(@"src\sol\b\Second.cs", new HunkRange(2, 1)) };
        var shownSymbols = new[] { SymbolEntry(@"b\Second.cs", 1, 5), SymbolEntry(@"a\first.cs", 4, 4) };
        var violations = new[]
        {
            Vio(@"C:\repo\src\sol\b\Second.cs", 2, "RB"),
            Vio(@"C:\repo\src\sol\b\Second.cs", 1, "RA"),
            Vio(@"C:\REPO\SRC\SOL\a\FIRST.cs", 4, "RC")
        };

        var result = RunFilter(violations, changedFiles, shownSymbols);

        // Sortierung FilePath ordinal case-insensitive: ...\a\FIRST.cs < ...\b\Second.cs; je Datei Zeile, dann Regel.
        Assert.Equal(new[] { "RC", "RA", "RB" }, result.Select(v => v.RuleName));
        Assert.Equal(1, result.Count(v => v.LineNumber == 2 && v.RuleName == "RB"));
    }

    [Fact]
    public async Task Collect_RealLinterEngine_CountsExactlyOneLintRun_AndFiltersByHunk()
    {
        using var testSolution = ChangeContextScenarioFactory.CreateScenario();
        var counters = new DiffImpactCounters();

        var result = await DiffViolationScanner.CollectAsync(new DiffViolationScanRequest(
            testSolution.Solution,
            CreateScenarioLintConfig(),
            new SilentLintConsole(),
            ScenarioSolutionDirectory(),
            [new ChangedFileRange("App/OrderService.cs", [new HunkRange(ScenarioOrderClassLine, 1)])],
            [],
            counters));

        Assert.False(result.IsMalfunction);
        Assert.Equal(1, counters.LintRuns);

        var orderViolations = result.Violations
            .Where(v => v.FilePath.EndsWith("OrderService.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var orderViolation = Assert.Single(orderViolations);
        Assert.Equal(nameof(GlobalConfig.EnforceSealedClasses), orderViolation.RuleName);
        Assert.Equal(ScenarioOrderClassLine, orderViolation.LineNumber);

        // Dieselbe Regel trifft AuditLogger.cs ebenfalls — ohne Hunk/Spanne bleibt diese Datei komplett außen vor.
        Assert.DoesNotContain(result.Violations, v => v.FilePath.EndsWith("AuditLogger.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Collect_EmptyInput_SkipsLint_NoCounterIncrement_NoMalfunction()
    {
        using var testSolution = ChangeContextScenarioFactory.CreateScenario();
        var counters = new DiffImpactCounters();

        var result = await DiffViolationScanner.CollectAsync(new DiffViolationScanRequest(
            testSolution.Solution,
            CreateScenarioLintConfig(),
            new SilentLintConsole(),
            ScenarioSolutionDirectory(),
            [],
            [],
            counters));

        Assert.Empty(result.Violations);
        Assert.False(result.IsMalfunction);
        Assert.Null(result.Context);
        Assert.Equal(0, counters.LintRuns);
    }

    private sealed class SilentLintConsole : ILintConsole
    {
        public void WriteLine(string message) { }
        public void WriteError(string message) { }
    }
}
