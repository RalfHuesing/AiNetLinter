#nullable enable

using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Core.DuplicateDetection;

[Trait("Category", "Component")]
public sealed partial class DuplicateDetectionEngineTests
{
    private static readonly DuplicateDetectionOptions DefaultOptions = new(
        MinTokens: DuplicateDetectionDefaults.MinTokens,
        NgramSize: DuplicateDetectionDefaults.NgramSize,
        MinSharedNgrams: DuplicateDetectionDefaults.MinSharedNgrams,
        ExactThreshold: DuplicateDetectionDefaults.ExactThreshold,
        NearThreshold: DuplicateDetectionDefaults.NearThreshold,
        FuzzyThreshold: DuplicateDetectionDefaults.FuzzyThreshold,
        NormalizeIdentifiers: false);

    [Fact]
    public async Task ScanAsync_ByteIdenticalBodies_ClassifiesAsExact()
    {
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeOne")),
            ("B.cs", TestHelper.BuildCalibratedMethod("B", "ComputeTwo")));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(2, cluster.Members.Count);
        Assert.Equal(1.0, cluster.Score, precision: 6);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_OneStatementChanged_ClassifiesAsNear()
    {
        var variant = WithReplacedStatements([8], ["int i = a * 7;"]);
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildMethod("B", "ComputeNear", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.InRange(cluster.Score, 0.80, 0.9499);
        Assert.Equal(DuplicateSimilarityBucket.Near, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_TwoNonOverlappingStatementsChanged_ClassifiesAsFuzzy()
    {
        var variant = WithReplacedStatements(
            [1, 6, 11, 17],
            ["int b = x * 9;", "int g = c * 9;", "int l = k * 9;", "int r = q * 9;"]);
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildMethod("B", "ComputeFuzzy", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.InRange(cluster.Score, 0.65, 0.7999);
        Assert.Equal(DuplicateSimilarityBucket.Fuzzy, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_SixStatementsChanged_FallsBelowFuzzyThreshold_NoCluster()
    {
        var variant = WithReplacedStatements(
            [0, 3, 6, 9, 12, 18],
            ["int a = x * 11;", "int d = x * 12;", "int g = a * 13;", "int j = a * 14;", "int m = a * 15;", "int s = a * 16;"]);
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildMethod("B", "ComputeDifferent", variant)));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_ChainOfSimilarMethods_FormsSingleTransitiveCluster()
    {
        var variantB = WithReplacedStatements([8], ["int i = a * 7;"]);
        var variantC = WithReplacedStatements([14], ["int o = a * 8;"]);
        using var testSolution = CreateSolution(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "ComputeBase")),
            ("B.cs", BuildMethod("B", "ComputeNearB", variantB)),
            ("C.cs", BuildMethod("C", "ComputeNearC", variantC)));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(3, cluster.Members.Count);
    }

    [Fact]
    public async Task ScanAsync_TwoDisposeMethodsWithDifferentFields_NoCluster()
    {
        const string classA = """
            public sealed class ResourceA : System.IDisposable
            {
                private System.IO.Stream? _stream;
                private System.Threading.Mutex? _mutex;
                private bool _disposed;
                public void Dispose()
                {
                    if (_disposed) return;
                    _stream?.Flush(); _stream?.Dispose(); _stream = null;
                    _mutex?.ReleaseMutex(); _mutex?.Dispose(); _mutex = null;
                    _disposed = true;
                }
            }
            """;
        const string classB = """
            public sealed class ResourceB : System.IDisposable
            {
                private System.Net.Sockets.Socket? _socket;
                private System.Timers.Timer? _timer;
                private bool _closed;
                public void Dispose()
                {
                    if (_closed) return;
                    _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); _socket?.Close(); _socket = null;
                    _timer?.Stop(); _timer?.Dispose(); _timer = null;
                    _closed = true;
                }
            }
            """;
        using var testSolution = CreateSolution(("A.cs", classA), ("B.cs", classB));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ScanAsync_StructurallySimilarButSemanticallyDifferentPairs_NoCluster(int pairIndex)
    {
        var pairs = new (string A, string B)[]
        {
            (BuildCustomMethod("ValidatorA", "Validate", ["if (x < 0) { return -1; }", "int p1 = x + 100;", "int p2 = p1 * 3;", "int p3 = p2 - 7;", "int p4 = p3 / 2;", "return p4;"]), BuildCustomMethod("ValidatorB", "Validate", ["if (x > 1000) { return -2; }", "int q1 = x - 50;", "int q2 = q1 / 4;", "int q3 = q2 + 11;", "int q4 = q3 * 9;", "return q4;"])),
            (BuildCustomMethod("ParserA", "Parse", ["int p1 = x % 7;", "int p2 = p1 ^ 3;", "int p3 = p2 << 1;", "int p4 = p3 | p1;", "int p5 = p4 + p2;", "return p5;"]), BuildCustomMethod("ParserB", "Parse", ["int q1 = x & 15;", "int q2 = q1 >> 2;", "int q3 = q2 + q1;", "int q4 = q3 % 5;", "int q5 = q4 - q2;", "return q5;"])),
            (BuildCustomMethod("FormatterA", "Format", ["int p1 = x * x;", "int p2 = p1 + x;", "int p3 = p2 * p2;", "int p4 = p3 - p1;", "int p5 = p4 % 1000;", "return p5;"]), BuildCustomMethod("FormatterB", "Format", ["int q1 = x + 7;", "int q2 = q1 - 3;", "int q3 = q2 + 9;", "int q4 = q3 - 5;", "int q5 = q4 + 2;", "return q5;"])),
            (BuildCustomMethod("CacheA", "Lookup", ["int p1 = x * 2 + 1;", "int p2 = p1 % 97;", "int p3 = p2 + 13;", "int p4 = p3 * 17;", "int p5 = p4 - p1;", "return p5;"]), BuildCustomMethod("CacheB", "Lookup", ["int q1 = x / 3 - 1;", "int q2 = q1 % 53;", "int q3 = q2 - 19;", "int q4 = q3 * 23;", "int q5 = q4 + q1;", "return q5;"])),
        };

        var (bodyA, bodyB) = pairs[pairIndex];
        using var testSolution = CreateSolution(("A.cs", bodyA), ("B.cs", bodyB));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_FourMethodsInstantiatingSameOptionsObject_DetectedAsOneCluster()
    {
        const string stubTypes = """
            public sealed class SerializerOptionsStub { public NamingPolicyStub? PropertyNamingPolicy { get; set; } public bool WriteIndented { get; set; } public bool IgnoreNullValues { get; set; } public int MaxDepth { get; set; } public EncoderStub? Encoder { get; set; } }
            public sealed class NamingPolicyStub { public static NamingPolicyStub CamelCase { get; } = new(); }
            public sealed class EncoderStub { public static EncoderStub Default { get; } = new(); }
            """;
        static string BuildOptionsMethod(string className, string methodName) => $$"""
            public static class {{className}}
            {
                public static SerializerOptionsStub {{methodName}}()
                {
                    var options = new SerializerOptionsStub { PropertyNamingPolicy = NamingPolicyStub.CamelCase, WriteIndented = false, IgnoreNullValues = true, MaxDepth = 32, Encoder = EncoderStub.Default };
                    return options;
                }
            }
            """;
        using var testSolution = CreateSolution(
            ("Stubs.cs", stubTypes),
            ("HandlerA.cs", BuildOptionsMethod("HandlerA", "BuildOptions")),
            ("HandlerB.cs", BuildOptionsMethod("HandlerB", "BuildOptions")),
            ("HandlerC.cs", BuildOptionsMethod("HandlerC", "BuildOptions")),
            ("HandlerD.cs", BuildOptionsMethod("HandlerD", "BuildOptions")));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(4, cluster.Members.Count);
        Assert.True(cluster.Bucket is DuplicateSimilarityBucket.Exact or DuplicateSimilarityBucket.Near);
    }

    [Fact]
    public async Task ScanAsync_IdenticalLocalFunctions_FormCluster()
    {
        const string localFunctions = """
            public static class LocalFunctions
            {
                public static int First(int x)
                {
                    static int ComputeOne(int value)
                    {
                        int a = value + 1; int b = value + 2; int c = value + 3; int d = value + 4; int e = value + 5;
                        int f = a + b; int g = c + d; int h = e + f; int i = g + h; int j = i - a;
                        int k = j - b; int l = k - c; int m = l - d; int n = m - e; int o = n * 2;
                        return o;
                    }
                    return ComputeOne(x);
                }
                public static int Second(int x)
                {
                    static int ComputeTwo(int value)
                    {
                        int a = value + 1; int b = value + 2; int c = value + 3; int d = value + 4; int e = value + 5;
                        int f = a + b; int g = c + d; int h = e + f; int i = g + h; int j = i - a;
                        int k = j - b; int l = k - c; int m = l - d; int n = m - e; int o = n * 2;
                        return o;
                    }
                    return ComputeTwo(x);
                }
            }
            """;
        using var testSolution = CreateSolution(("LocalFunctions.cs", localFunctions));

        var result = await DuplicateDetectionEngine.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(4, cluster.Members.Count);
        Assert.Contains(cluster.Members, member => member.SignatureName.Contains("ComputeOne", StringComparison.Ordinal));
        Assert.Contains(cluster.Members, member => member.SignatureName.Contains("ComputeTwo", StringComparison.Ordinal));
    }

    private static RoslynTestSolution CreateSolution(params (string FileName, string Content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionEngineTests.slnx",
            new ProjectSpec("DuplicateDetectionEngineCases", files));

    private static string[] WithReplacedStatements(int[] indices, string[] replacements)
    {
        var copy = (string[])TestHelper.CalibratedBaseStatements.Clone();
        for (var index = 0; index < indices.Length; index++) copy[indices[index]] = replacements[index];
        return copy;
    }

    private static string BuildMethod(string className, string methodName, IReadOnlyList<string> statements)
    {
        var body = string.Join("\n        ", statements);
        return $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    {{body}}
                    return t;
                }
            }
            """;
    }

    private static string BuildCustomMethod(string className, string methodName, IReadOnlyList<string> statements)
    {
        var body = string.Join("\n        ", statements);
        return $$"""
            public static class {{className}}
            {
                public static int {{methodName}}(int x)
                {
                    {{body}}
                }
            }
            """;
    }
}
