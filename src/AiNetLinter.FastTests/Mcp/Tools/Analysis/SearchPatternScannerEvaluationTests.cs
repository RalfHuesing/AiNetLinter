#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

[Trait("Category", "Unit")]
public sealed class SearchPatternScannerEvaluationTests
{
    private readonly ITestOutputHelper output;

    public SearchPatternScannerEvaluationTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void Scan_OracleAndBudgetCases_ExplainEveryVisibleLoss()
    {
        using var fixture = EvaluationFixture.Create();
        var oracle = Scan(fixture.Workspace, "search-anchor");
        Assert.Equal(new[] { "src/SymbolGraphMini/search-fixture.md", "src/SymbolGraphMini/wwwroot/search-fixture.json" },
            oracle.Payload.Matches.Select(match => match.FilePath));
        Assert.Equal(2, oracle.Payload.Completeness.TotalMatchedLineCount);
        Assert.Equal(2, oracle.Payload.Completeness.MatchedFileCount);
        Assert.Equal(2, oracle.Payload.Matches[0].MatchRanges.Count);
        Assert.Equal(new SearchPatternMatchRange(1, 13), oracle.Payload.Matches[0].MatchRanges[0]);
        Assert.Equal(new SearchPatternMatchRange(15, 13), oracle.Payload.Matches[0].MatchRanges[1]);

        var context = Scan(fixture.Workspace, "search-anchor", new() { ContextLines = 1 });
        var contextMatch = Assert.Single(context.Payload.Matches, match => match.FilePath.EndsWith(".md", StringComparison.Ordinal));
        Assert.Equal(new[] { "context-before" }, contextMatch.ContextBefore);
        Assert.Equal(new[] { "context-after" }, contextMatch.ContextAfter);

        var regex = Scan(fixture.Workspace, "search-anchor", new() { IsRegex = true });
        Assert.Equal(oracle.Payload.Matches.Select(match => match.FilePath), regex.Payload.Matches.Select(match => match.FilePath));
        Assert.Equal(oracle.Payload.Matches.Select(match => match.MatchRanges.Count), regex.Payload.Matches.Select(match => match.MatchRanges.Count));

        var maxResults = Scan(fixture.Workspace, "search-anchor", new() { MaxResults = 1 });
        var maxFiles = Scan(fixture.Workspace, "search-anchor", new() { MaxFiles = 1 });
        var maxBytes = Scan(fixture.Workspace, "search-anchor", new() { MaxResponseBytes = 200 });
        AssertBudgetExplained(oracle, maxResults, "maxResults");
        AssertBudgetExplained(oracle, maxFiles, "maxFiles");
        AssertBudgetExplained(oracle, maxBytes, "maxResponseBytes");
        Assert.Equal(1, maxResults.Payload.Completeness.ShownMatchedLineCount);
        Assert.Equal(1, maxFiles.Payload.Completeness.ShownMatchedFileCount);

        WriteTiming("plain-oracle", () => Scan(fixture.Workspace, "search-anchor"));
        WriteTiming("regex-oracle", () => Scan(fixture.Workspace, "search-anchor", new() { IsRegex = true }));
        WriteTiming("max-response-bytes", () => Scan(fixture.Workspace, "search-anchor", new() { MaxResponseBytes = 200 }));
    }

    [Fact]
    public void Scan_OverlayProblemFiles_ReportSkipsAndDefaultExclusions()
    {
        using var fixture = EvaluationFixture.Create();
        fixture.CreateFile("visible.txt", "problem-anchor");
        fixture.CreateFile("obj/Debug/Generated.cs", "problem-anchor");
        fixture.CreateFile("Generated.g.cs", "problem-anchor");
        fixture.CreateFile("bundle.min.js", "problem-anchor");
        fixture.CreateBytes("binary-anchor.bin", [0, 1, 0, 2]);
        fixture.CreateBytes("invalid-encoding.dat", [0xC3]);

        var oracle = Scan(fixture.Workspace, "problem-anchor");
        Assert.Single(oracle.Payload.Matches);
        Assert.Equal("visible.txt", oracle.Payload.Matches[0].FilePath);
        Assert.Equal(1, oracle.Payload.Completeness.SkippedBinaryFileCount);
        Assert.Equal(1, oracle.Payload.Completeness.SkippedUnreadableFileCount);
        Assert.DoesNotContain(oracle.Payload.Matches, match => match.FilePath.Contains("Generated", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(oracle.Payload.Matches, match => match.FilePath.Contains("obj/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(oracle.Payload.Matches, match => match.FilePath.Contains(".min.", StringComparison.OrdinalIgnoreCase));

    }

    [Fact]
    public async Task Scan_EnrichmentAndCancellation_KeepLexicalFactsVisible()
    {
        using var fixture = EvaluationFixture.Create();
        var enriched = await SearchPatternScannerEnrichment.ScanAsync(
            Parameters(fixture.Workspace, "Greeter", new() { EnrichCSharp = true }));
        var declaration = Assert.Single(enriched.Payload.Matches, match => match.Line == 3);
        Assert.Equal("declaration", declaration.Semantic!.Kind);
        Assert.Equal("resolved", declaration.Semantic.Resolution);

        using var preCancellation = new CancellationTokenSource();
        preCancellation.Cancel();
        var preCancelled = SearchPatternScanner.Scan(
            Parameters(fixture.Workspace, "search-anchor", new() { CancellationToken = preCancellation.Token }));
        Assert.Empty(preCancelled.Payload.Matches);
        Assert.True(preCancelled.Payload.Completeness.CancellationRequested);
        Assert.Contains("cancellation", preCancelled.Payload.Completeness.TruncatedBy);

        using var postCancellation = new CancellationTokenSource();
        var parameters = Parameters(fixture.Workspace, "Greeter", new()
        {
            EnrichCSharp = true,
            CancellationToken = postCancellation.Token,
        });
        async Task<IReadOnlyList<SearchPatternMatch>> CancelDuringEnrichment(
            Solution solution,
            IReadOnlyList<SearchPatternMatch> matches,
            CancellationToken token)
        {
            _ = solution;
            _ = matches;
            postCancellation.Cancel();
            await Task.Yield();
            return await Task.FromCanceled<IReadOnlyList<SearchPatternMatch>>(token);
        }

        var postCancelled = await SearchPatternScannerEnrichment.ScanAsync(
            parameters,
            SearchPatternScanner.Scan,
            CancelDuringEnrichment);
        Assert.NotEmpty(postCancelled.Payload.Matches);
        Assert.All(postCancelled.Payload.Matches, match => Assert.Null(match.Semantic));
        Assert.True(postCancelled.Payload.Completeness.CancellationRequested);
        Assert.False(postCancelled.Payload.Completeness.ScanCompleted);
        Assert.Contains("cancellation", postCancelled.Payload.Completeness.TruncatedBy);
    }

    [Fact]
    public void Scan_RegexTimeout_IsRecoverableAndExplained()
    {
        using var fixture = EvaluationFixture.Create();
        fixture.CreateFile("large-search.txt", new string('a', 100_000) + "!");

        var result = Scan(fixture.Workspace, "^(a+)+$", new() { IsRegex = true });
        Assert.True(result.Payload.Completeness.RegexTimedOut);
        Assert.False(result.Payload.Completeness.ScanCompleted);
        Assert.Contains("regexTimeout", result.Payload.Completeness.TruncatedBy);
        Assert.Contains("regexTimeout", result.Payload.Completeness.TruncatedBy);
    }

    private void WriteTiming(string caseId, Func<SearchPatternScanResult> scan)
    {
        scan();
        var durations = Enumerable.Range(0, 7)
            .Select(_ =>
            {
                var stopwatch = Stopwatch.StartNew();
                scan();
                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            })
            .OrderBy(value => value)
            .ToArray();
        output.WriteLine($"{caseId}: warmup=1 iterations=7 min={durations[0]:F3} median={durations[3]:F3} p95={durations[^1]:F3} ms");
    }

    private static void AssertBudgetExplained(
        SearchPatternScanResult oracle,
        SearchPatternScanResult budgeted,
        string reason)
    {
        var completeness = budgeted.Payload.Completeness;
        Assert.Contains(reason, completeness.TruncatedBy);
        Assert.True(completeness.ShownMatchedLineCount <= oracle.Payload.Completeness.TotalMatchedLineCount);
        Assert.True(completeness.ShownMatchedFileCount <= oracle.Payload.Completeness.MatchedFileCount);
        Assert.True(completeness.Truncated || completeness.CancellationRequested || completeness.RegexTimedOut);
    }

    private static SearchPatternScanResult Scan(
        Solution solution,
        string pattern,
        SearchPatternEvaluationOptions? options = null) =>
        SearchPatternScanner.Scan(Parameters(solution, pattern, options ?? new()));

    private static SearchPatternScannerParameters Parameters(
        Solution solution,
        string pattern,
        SearchPatternEvaluationOptions options) =>
        new(
            solution,
            pattern,
            options.IsRegex,
            options.MaxResults,
            options.MaxFiles,
            options.ContextLines,
            options.MaxResponseBytes,
            options.Scope,
            options.IncludePatterns,
            options.ExcludePatterns,
            options.CancellationToken,
            options.EnrichCSharp);

    private sealed record SearchPatternEvaluationOptions
    {
        internal bool IsRegex { get; init; }
        internal int MaxResults { get; init; }
        internal int MaxFiles { get; init; }
        internal int ContextLines { get; init; }
        internal int MaxResponseBytes { get; init; }
        internal string? Scope { get; init; }
        internal IReadOnlyList<string>? IncludePatterns { get; init; }
        internal IReadOnlyList<string>? ExcludePatterns { get; init; }
        internal CancellationToken CancellationToken { get; init; }
        internal bool EnrichCSharp { get; init; }
    }

    private sealed class EvaluationFixture : IDisposable
    {
        private readonly IsolatedFixtureLease lease;

        private EvaluationFixture(IsolatedFixtureLease lease, RoslynTestSolution solution)
        {
            this.lease = lease;
            Solution = solution;
        }

        internal string RootPath => lease.RootPath;
        internal RoslynTestSolution Solution { get; }
        internal Microsoft.CodeAnalysis.Solution Workspace => Solution.Solution;

        internal static EvaluationFixture Create()
        {
            var lease = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini", "SearchPatternEvaluation_");
            var projectDirectory = Path.Combine(lease.RootPath, "src", "SymbolGraphMini");
            var documents = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => (Path.GetFileName(path), File.ReadAllText(path)))
                .ToArray();
            var solution = RoslynTestSolutionFactory.CreateSolution(
                Path.Combine(lease.RootPath, "SymbolGraphMini.slnx"),
                new ProjectSpec("SymbolGraphMini", documents, VirtualProjectDirectory: Path.Combine("src", "SymbolGraphMini")));
            return new EvaluationFixture(lease, solution);
        }

        internal string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        internal string CreateBytes(string relativePath, byte[] content)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            Solution.Dispose();
            lease.Dispose();
        }
    }
}
