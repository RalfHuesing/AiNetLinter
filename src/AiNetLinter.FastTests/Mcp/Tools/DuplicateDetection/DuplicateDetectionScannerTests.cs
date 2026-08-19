#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DuplicateDetection;

[Trait("Category", "Component")]
public sealed class DuplicateDetectionScannerTests
{
    [Fact]
    public async Task ScanAsync_DefaultFuzzyThreshold_ReturnsExactCluster()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
                ("B.cs", TestHelper.BuildCalibratedMethod("B", "Two")),
            ]));

        var result = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), new DuplicateDetectionInput(null, null, null, null, null),
            DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        var cluster = Assert.Single(result.ShownClusters);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
        Assert.False(result.Truncated);
        Assert.Equal(1, result.TotalClusters);
    }

    [Fact]
    public async Task ScanAsync_ExactThresholdFilter_ExcludesLowerBuckets()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
                ("B.cs", BuildVariantMethod("B", "Two", [8], ["int i = a * 7;"])),
            ]));
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        var available = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);
        var filtered = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Exact, CancellationToken.None);

        Assert.Equal(DuplicateSimilarityBucket.Near, Assert.Single(available.ShownClusters).Bucket);
        Assert.Empty(filtered.ShownClusters);
        Assert.Equal(0, filtered.TotalClusters);
    }

    [Fact]
    public async Task ScanAsync_NearThresholdFilter_ExcludesFuzzyOnlyCluster()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
                ("B.cs", BuildVariantMethod(
                    "B", "Two", [1, 6, 11, 17],
                    ["int b = x * 9;", "int g = c * 9;", "int l = k * 9;", "int r = q * 9;"])),
            ]));
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        var available = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);
        var filtered = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Near, CancellationToken.None);

        Assert.Equal(DuplicateSimilarityBucket.Fuzzy, Assert.Single(available.ShownClusters).Bucket);
        Assert.Empty(filtered.ShownClusters);
        Assert.Equal(0, filtered.TotalClusters);
    }

    [Fact]
    public async Task ScanAsync_MaxResultsFromInput_OverridesConfigDefault()
    {
        var distinctMethod = BuildVariantMethod(
            "B1", "F1", [0, 3, 6, 9, 12, 18],
            ["int a = x * 11;", "int d = x * 12;", "int g = a * 13;", "int j = a * 14;", "int m = a * 15;", "int s = a * 16;"]);
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("A1.cs", TestHelper.BuildCalibratedMethod("A1", "F1")),
                ("A2.cs", TestHelper.BuildCalibratedMethod("A2", "F2")),
                ("B1.cs", distinctMethod),
                ("B2.cs", distinctMethod.Replace("class B1", "class B2", System.StringComparison.Ordinal)),
            ]));
        var input = new DuplicateDetectionInput(null, null, null, null, MaxResults: 1);

        var result = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig { DuplicateCodeMaxResults = 2 }, input,
            DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Single(result.ShownClusters);
        Assert.Equal(2, result.TotalClusters);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanAsync_MinTokensFromInput_ExcludesShortMethodEvenIfConfigAllowsIt()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("A.cs", "public static class A { public static int One() => 1 + 1 + 1 + 1 + 1 + 1; }"),
                ("B.cs", "public static class B { public static int Two() => 1 + 1 + 1 + 1 + 1 + 1; }"),
            ]));
        var strictInput = new DuplicateDetectionInput(MinTokens: 100, null, null, null, null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig { DuplicateCodeMinTokens = 5 }, strictInput,
            DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_ScopeDirWithForwardSlashes_MatchesWindowsPaths()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", [
                ("Included/A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
                ("Excluded/B.cs", TestHelper.BuildCalibratedMethod("B", "Two")),
            ]));
        var input = new DuplicateDetectionInput(null, null, null, ScopeDir: "Included/", null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_EmptySolution_ReturnsEmptyResultWithoutError()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionScannerTests.slnx",
            new ProjectSpec("ScannerCases", []));

        var result = await DuplicateDetectionScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), new DuplicateDetectionInput(null, null, null, null, null),
            DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
        Assert.Equal(0, result.MethodsScanned);
        Assert.False(result.Truncated);
    }

    private static string BuildVariantMethod(
        string className,
        string methodName,
        IReadOnlyList<int> replacementIndexes,
        IReadOnlyList<string> replacements)
    {
        var statements = (string[])TestHelper.CalibratedBaseStatements.Clone();
        for (var index = 0; index < replacementIndexes.Count; index++)
        {
            statements[replacementIndexes[index]] = replacements[index];
        }

        return $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    {{string.Join("\n            ", statements)}}
                    return t;
                }
            }
            """;
    }
}
