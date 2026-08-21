#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

[Trait("Category", "Unit")]
public sealed class SearchPatternScannerTests
{
    [Fact]
    public void Scan_PlainText_EmitsAllMatchRangesAndStablePositions()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "matches.txt");
        File.WriteAllText(path, "before\r\n  Anchor anchor  \r\nafter");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { ContextLines = 1 }));
        var match = Assert.Single(result.Payload.Matches);

        Assert.Equal("src/Project/matches.txt", match.FilePath);
        Assert.Equal(2, match.Line);
        Assert.Equal("  Anchor anchor  ", match.LineText);
        Assert.Equal(new[] { new SearchPatternMatchRange(3, 6), new SearchPatternMatchRange(10, 6) }, match.MatchRanges);
        Assert.Equal(new[] { "before" }, match.ContextBefore);
        Assert.Equal(new[] { "after" }, match.ContextAfter);
        Assert.Equal("Project", match.ProjectName);
    }

    [Fact]
    public void Scan_RegexUsesSameRangeModelAndStableOrdering()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("^anchor$") { IsRegex = true }));

        Assert.Equal(new[] { "src/Project/a.txt", "src/Project/b.txt" }, result.Payload.Matches.Select(m => m.FilePath));
        Assert.Equal(new[] { new SearchPatternMatchRange(1, 6) }, result.Payload.Matches[0].MatchRanges);
    }

    [Fact]
    public void Scan_ContextLines_PreservesUnchangedLineText()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllText(
            Path.Combine(tempDir.DirectoryPath, "src", "Project", "context.txt"),
            "one\n two  \nthree\nfour");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("two") { ContextLines = 1 }));
        var match = Assert.Single(result.Payload.Matches);

        Assert.Equal(" two  ", match.LineText);
        Assert.Equal(new[] { "one" }, match.ContextBefore);
        Assert.Equal(new[] { "three" }, match.ContextAfter);
    }

    [Fact]
    public void Scan_MaxFilesAndMaxResponseBytes_ReportSeparateTruncationReasons()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");

        var fileLimited = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { MaxFiles = 1 }));
        var byteLimited = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor") { MaxResponseBytes = 200 }));

        Assert.Contains("maxFiles", fileLimited.Payload.Completeness.TruncatedBy);
        Assert.DoesNotContain("maxResponseBytes", fileLimited.Payload.Completeness.TruncatedBy);
        Assert.Contains("maxResponseBytes", byteLimited.Payload.Completeness.TruncatedBy);
        Assert.Equal(2, byteLimited.Payload.Completeness.TotalMatchedLineCount);
    }

    [Fact]
    public void Scan_MaxResults_CountsOnlyVisibleMatchedFiles()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var projectDir = Path.Combine(tempDir.DirectoryPath, "src", "Project");
        File.WriteAllText(Path.Combine(projectDir, "a.txt"), "anchor\nanchor");
        File.WriteAllText(Path.Combine(projectDir, "b.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { MaxResults = 1 }));

        Assert.Single(result.Payload.Matches);
        Assert.Equal(1, result.Payload.Completeness.ShownMatchedFileCount);
        Assert.Equal(3, result.Payload.Completeness.TotalMatchedLineCount);
        Assert.Contains("maxResults", result.Payload.Completeness.TruncatedBy);
    }

    [Fact]
    public void Scan_ScopeFiltersAndDefaultExclusions_StayInsideSolutionRoot()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "root.txt"), "anchor");
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "src", "Project", "scope.json"), "anchor");
        Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "obj"));
        File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "obj", "generated.txt"), "anchor");

        var result = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { Scope = "src", IncludePatterns = new[] { "**/*.json" } }));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Equal("src/Project/scope.json", match.FilePath);
        Assert.DoesNotContain(result.Payload.Matches, m => m.FilePath.Contains("obj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scan_BinaryUnreadableAndCancelledFiles_AreReflectedInCompleteness()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        File.WriteAllBytes(Path.Combine(tempDir.DirectoryPath, "binary.dat"), new byte[] { 0, 1, 2 });
        File.WriteAllBytes(Path.Combine(tempDir.DirectoryPath, "invalid.txt"), new byte[] { 0xFF, 0xFF });

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("anchor")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelledResult = SearchPatternScanner.Scan(CreateParameters(
            solution.Solution,
            new("anchor") { CancellationToken = cancelled.Token }));

        Assert.Equal(1, result.Payload.Completeness.SkippedBinaryFileCount);
        Assert.Equal(1, result.Payload.Completeness.SkippedUnreadableFileCount);
        Assert.Contains("cancellation", cancelledResult.Payload.Completeness.TruncatedBy);
        Assert.False(cancelledResult.Payload.Completeness.ScanCompleted);
    }

    private static RoslynTestSolution CreateSolution(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "Project"));
        return RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(root, "Fixture.slnx"),
            new ProjectSpec(
                "Project",
                [("Project.cs", "namespace Project; public sealed class ProjectType { }")],
                VirtualProjectDirectory: Path.Combine("src", "Project")));
    }

    private static SearchPatternScannerParameters CreateParameters(
        Microsoft.CodeAnalysis.Solution solution,
        SearchPatternTestOptions options) =>
        new(
            solution,
            options.Pattern,
            options.IsRegex,
            options.MaxResults,
            options.MaxFiles,
            options.ContextLines,
            options.MaxResponseBytes,
            options.Scope,
            options.IncludePatterns,
            null,
            options.CancellationToken);

    private sealed record SearchPatternTestOptions(string Pattern)
    {
        internal bool IsRegex { get; init; }
        internal int MaxResults { get; init; } = 50;
        internal int MaxFiles { get; init; }
        internal int ContextLines { get; init; }
        internal int MaxResponseBytes { get; init; }
        internal string? Scope { get; init; }
        internal string[]? IncludePatterns { get; init; }
        internal CancellationToken CancellationToken { get; init; }
    }
}
