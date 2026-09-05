#nullable enable

using System.IO;
using System.Threading;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

[Trait("Category", "Unit")]
public sealed class SearchPatternPromotionTests
{
    [Fact]
    public void Scan_ZeroPlainHitsWithWildcard_AutoPromotesToRegexAndFindsHits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-autopromote-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Customer.cs");
        File.WriteAllText(path, "public class CustomerService {}");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("*Service")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("CustomerService", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithMethodParentheses_AutoPromotesToMethodCallRegex()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-method-paren-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Worker.cs");
        File.WriteAllText(path, "public async Task<int> CalculateAsync(int x, int y) { return x + y; }");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("CalculateAsync()")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("CalculateAsync(int x, int y)", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithGenericTypeParameter_AutoPromotesToGenericRegex()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-generic-type-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Repo.cs");
        File.WriteAllText(path, "public sealed class ResultRepository : IRepository<Customer> { }");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("IRepository<T>")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("IRepository<Customer>", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ZeroPlainHitsWithQuotedOrBacktickedPattern_AutoPromotesAndFindsHits()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-quoted-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Service.cs");
        File.WriteAllText(path, "public sealed class OrderProcessor { }");

        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("`OrderProcessor`")));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("OrderProcessor", match.LineText);
        Assert.True(result.IsRegexAutoPromoted);
    }

    [Fact]
    public void Scan_ScopeWithAbsolutePathInsideSolutionRoot_NormalizesAndMatches()
    {
        using var tempDir = TestTempDirectory.Create("search-pattern-absolute-scope-");
        using var solution = CreateSolution(tempDir.DirectoryPath);
        var path = Path.Combine(tempDir.DirectoryPath, "src", "Project", "Code.cs");
        File.WriteAllText(path, "string key = \"TargetValue\";");

        var absoluteScope = Path.Combine(tempDir.DirectoryPath, "src");
        var result = SearchPatternScanner.Scan(CreateParameters(solution.Solution, new("TargetValue") { Scope = absoluteScope }));

        var match = Assert.Single(result.Payload.Matches);
        Assert.Contains("TargetValue", match.LineText);
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
        Solution solution,
        SearchPatternPromotionTestOptions options) =>
        new(
            solution,
            options.Pattern,
            options.IsRegex,
            options.MaxResults,
            0,
            0,
            0,
            options.Scope,
            null,
            null,
            CancellationToken.None,
            false,
            null);

    private sealed record SearchPatternPromotionTestOptions(string Pattern)
    {
        internal bool? IsRegex { get; init; }
        internal int MaxResults { get; init; } = 50;
        internal string? Scope { get; init; }
    }
}
