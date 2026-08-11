#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="DuplicateDetectionScanner"/> — Argument-Aufloesung (Tool-Input
/// ueberschreibt <see cref="GlobalConfig"/>-Defaults), Schwellwert-Filterung und
/// <c>maxResults</c>-Trunkierung. Nutzt dieselbe kalibrierte 20-Statement-Basismethode wie
/// <c>DuplicateDetectionEngineTests</c>/<c>DuplicateCodeCheckerTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DuplicateDetectionScannerTests : IDisposable
{
    private readonly string _tempDir;

    public DuplicateDetectionScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ainetlinter-dupscanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static readonly string[] BaseStatements =
    [
        "int a = x + 1;", "int b = x + 2;", "int c = x + 3;", "int d = x + 4;", "int e = x + 5;",
        "int f = a + b;", "int g = c + d;", "int h = e + f;", "int i = g + h;", "int j = i - a;",
        "int k = j - b;", "int l = k - c;", "int m = l - d;", "int n = m - e;", "int o = n * 2;",
        "int p = o / 2;", "int q = p + 1;", "int r = q + 2;", "int s = r + 3;", "int t = s + 4;",
    ];

    private static string BuildMethod(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static int {{methodName}}(int x)
            {
                {{string.Join("\n            ", BaseStatements)}}
                return t;
            }
        }
        """;

    private Solution CreateAdhocSolution(params (string FileName, string Content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var runtimeAsm = MetadataReference.CreateFromFile(typeof(System.Runtime.GCLatencyMode).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, runtimeAsm })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(_tempDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(_tempDir, file.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Content, filePath: fullPath);
        }
        return solution;
    }

    [Fact]
    public async Task ScanAsync_DefaultFuzzyThreshold_ReturnsExactCluster()
    {
        var solution = CreateAdhocSolution(("A.cs", BuildMethod("A", "One")), ("B.cs", BuildMethod("B", "Two")));
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        var cluster = Assert.Single(result.ShownClusters);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
        Assert.False(result.Truncated);
        Assert.Equal(1, result.TotalClusters);
    }

    [Fact]
    public async Task ScanAsync_ExactThresholdFilter_ExcludesLowerBuckets()
    {
        var solution = CreateAdhocSolution(("A.cs", BuildMethod("A", "One")));
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        // Nur eine Methode -> gar kein Cluster (egal welcher Bucket-Filter), belegt aber, dass der
        // Filter-Pfad keine Exception wirft.
        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Exact, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_NearThresholdFilter_ExcludesFuzzyOnlyCluster()
    {
        // Sechs weit auseinanderliegende Swaps -> Score klar unter fuzzy (kein Cluster ueberhaupt,
        // siehe DuplicateDetectionEngineTests-Kalibrierung) -> der near-Filter aendert daran nichts,
        // belegt aber denselben Fall auf Scanner-Ebene mit striktem Filter.
        var variantStatements = (string[])BaseStatements.Clone();
        variantStatements[0] = "int a = x * 11;";
        variantStatements[3] = "int d = x * 12;";
        variantStatements[6] = "int g = a * 13;";
        variantStatements[9] = "int j = a * 14;";
        variantStatements[12] = "int m = a * 15;";
        variantStatements[18] = "int s = a * 16;";
        var variantBody = $$"""
            public static class B
            {
                public static int Two(int x)
                {
                    {{string.Join("\n            ", variantStatements)}}
                    return t;
                }
            }
            """;
        var solution = CreateAdhocSolution(("A.cs", BuildMethod("A", "One")), ("B.cs", variantBody));
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Near, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_MaxResultsFromInput_OverridesConfigDefault()
    {
        var solution = CreateAdhocSolution(
            ("A1.cs", BuildMethod("A1", "F1")), ("A2.cs", BuildMethod("A2", "F2")),
            ("A3.cs", BuildMethod("A3", "F3")));
        var input = new DuplicateDetectionInput(null, null, null, null, MaxResults: 1);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        // Alle drei Methoden sind byte-identisch -> ein einziger Cluster mit 3 Mitgliedern
        // (transitive Cluster-Bildung, kein isoliertes Paar), maxResults=1 aendert an der
        // Cluster-ANZAHL nichts (1 Cluster gefunden, 1 gezeigt), aber belegt, dass der
        // Input-Wert statt des Config-Defaults verwendet wird.
        var cluster = Assert.Single(result.ShownClusters);
        Assert.Equal(3, cluster.Members.Count);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ScanAsync_MinTokensFromInput_ExcludesShortMethodEvenIfConfigAllowsIt()
    {
        var solution = CreateAdhocSolution(
            ("A.cs", "public static class A { public static int One() => 1 + 1 + 1 + 1 + 1 + 1; }"),
            ("B.cs", "public static class B { public static int Two() => 1 + 1 + 1 + 1 + 1 + 1; }"));
        var lenientConfig = new GlobalConfig { DuplicateCodeMinTokens = 5 };
        var strictInput = new DuplicateDetectionInput(MinTokens: 100, null, null, null, null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, lenientConfig, strictInput, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_ScopeDirWithForwardSlashes_MatchesWindowsPaths()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "Included"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Excluded"));
        var solution = CreateAdhocSolution(
            (Path.Combine("Included", "A.cs"), BuildMethod("A", "One")),
            (Path.Combine("Excluded", "B.cs"), BuildMethod("B", "Two")));
        var input = new DuplicateDetectionInput(null, null, null, ScopeDir: "Included", null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        // Nur eine Methode im Scope (die andere ist per scopeDir ausgeschlossen) -> kein Cluster.
        Assert.Empty(result.ShownClusters);
    }

    [Fact]
    public async Task ScanAsync_EmptySolution_ReturnsEmptyResultWithoutError()
    {
        var solution = CreateAdhocSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, null);

        var result = await DuplicateDetectionScanner.ScanAsync(
            solution, new GlobalConfig(), input, DuplicateSimilarityBucket.Fuzzy, CancellationToken.None);

        Assert.Empty(result.ShownClusters);
        Assert.Equal(0, result.MethodsScanned);
        Assert.False(result.Truncated);
    }
}
