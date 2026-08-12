#nullable enable

using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Core.DuplicateDetection;

public sealed partial class DuplicateDetectionEngineTests
{
    private static readonly IReadOnlyDictionary<string, string> IdentifierRenameMap =
        "abcdefghijklmnopqrst".ToCharArray()
            .Select((ch, index) => (Letter: ch.ToString(), Renamed: $"p{index + 1}"))
            .ToDictionary(item => item.Letter, item => item.Renamed);

    private static readonly System.Text.RegularExpressions.Regex IdentifierPattern =
        new(@"\b[a-t]\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    [Fact]
    public async Task ScanAsync_MethodsBelowMinTokenThreshold_NeverCluster()
    {
        const string source = """
            public static class TinyMethods { public static int One() => 1; public static int Two() => 1; }
            """;
        using var testSolution = CreateSolution(("A.cs", source));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(0, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_GeneratedCodeAttribute_SkipsMethod()
    {
        const string generated = """
            [System.CodeDom.Compiler.GeneratedCode("test", "1.0")]
            public static class GeneratedHolder
            {
                public static int ComputeOne(int x)
                {
                    int a = x + 1; int b = x + 2; int c = x + 3; int d = x + 4; int e = x + 5;
                    int f = a + b; int g = c + d; int h = e + f; int i = g + h; int j = i - a;
                    return j;
                }
            }
            """;
        var plain = BuildCustomMethod("PlainHolder", "ComputeTwo", [.. TestHelper.CalibratedBaseStatements[..10], "return j;"]);
        using var testSolution = CreateSolution(("Generated.cs", generated), ("Plain.cs", plain));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_ObjDirectory_IsExcluded()
    {
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeOne")),
            ("obj/B.cs", TestHelper.BuildCalibratedMethod("B", "ComputeTwo")));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_TestsFixturesDirectory_IsExcluded()
    {
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeOne")),
            ("tests/Fixtures/B.cs", TestHelper.BuildCalibratedMethod("B", "ComputeTwo")));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_RenamedIdentifiers_WithoutNormalization_NoCluster()
    {
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildRenamedBody()));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_RenamedIdentifiers_WithNormalization_DetectsAsClone()
    {
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildRenamedBody()));
        var options = DefaultOptions with { NormalizeIdentifiers = true };

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, options, CancellationToken.None);

        Assert.Equal(DuplicateSimilarityBucket.Exact, Assert.Single(result.Clusters).Bucket);
    }

    [Fact]
    public async Task ScanAsync_CustomThresholds_ChangeBucketClassification()
    {
        var variant = WithReplacedStatements([8], ["int i = a * 7;"]);
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildMethod("B", "ComputeNear", variant)));
        var lenientOptions = DefaultOptions with { ExactThreshold = 0.80 };

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, lenientOptions, CancellationToken.None);

        Assert.Equal(DuplicateSimilarityBucket.Exact, Assert.Single(result.Clusters).Bucket);
    }

    [Fact]
    public async Task ScanAsync_EmptySolution_ReturnsNoClusters()
    {
        using var testSolution = CreateSolution();

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(0, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_PathScopeFilter_ExcludesNonMatchingFiles()
    {
        using var testSolution = CreateSolution(
            ("Included/A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeOne")),
            ("Excluded/B.cs", TestHelper.BuildCalibratedMethod("B", "ComputeTwo")));
        var options = DefaultOptions with { PathScopeFilter = "Included" };

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, options, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    private static string RenameIdentifiers(string statement) =>
        IdentifierPattern.Replace(statement, match => IdentifierRenameMap[match.Value]);

    private static string BuildRenamedBody()
    {
        var renamed = TestHelper.CalibratedBaseStatements.Select(RenameIdentifiers).ToArray();
        return $$"""
            public static class Renamed
            {
                public static int ComputeRenamed(int x)
                {
                    {{string.Join("\n        ", renamed)}}
                    return p20;
                }
            }
            """;
    }
}
