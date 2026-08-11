#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.Tests.Core.DuplicateDetection;

/// <summary>
/// Fortsetzung von <see cref="DuplicateDetectionEngineTests"/> (<c>partial class</c>, siehe
/// Klassen-Doc-Kommentar in <c>DuplicateDetectionEngineTests.cs</c> fuer die Aufteilungs-Begruendung
/// und die Ground-Truth-Klonstufen-Tests): False-Positive-Disziplin (Min-Token-Filter,
/// GeneratedCode, Verzeichnis-Ausschluesse), Identifier-Normalisierung, konfigurierbare
/// Schwellwerte, sowie die gemeinsame Test-Infrastruktur (<see cref="CreateAdhocSolution"/>,
/// <see cref="TempSourceDirectory"/>).
/// </summary>
public sealed partial class DuplicateDetectionEngineTests
{
    // ── False-Positive-Disziplin: Min-Token-Filter, GeneratedCode, Verzeichnis-Ausschluesse ──

    [Fact]
    public async Task ScanAsync_MethodsBelowMinTokenThreshold_NeverCluster()
    {
        const string source = """
            public static class TinyMethods
            {
                public static int One() => 1;
                public static int Two() => 1;
            }
            """;
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("A.cs", source));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

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
        var plain = BuildCustomMethod("PlainHolder", "ComputeTwo",
            [.. BaseStatements[..10], "return j;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path, ("Generated.cs", generated), ("Plain.cs", plain));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        // Nur eine eligible Methode uebrig (die generierte wird uebersprungen) -> kein Cluster.
        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_ObjDirectory_IsExcluded()
    {
        using var dir = new TempSourceDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "obj"));
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeOne", BaseStatements)),
            (Path.Combine("obj", "B.cs"), BuildMethod("B", "ComputeTwo", BaseStatements)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_TestsFixturesDirectory_IsExcluded()
    {
        using var dir = new TempSourceDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "tests", "Fixtures"));
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeOne", BaseStatements)),
            (Path.Combine("tests", "Fixtures", "B.cs"), BuildMethod("B", "ComputeTwo", BaseStatements)));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    // ── Identifier-Normalisierung (Opt-in, Type-2-Klon-Erkennung) ────────────────────────────

    /// <summary>
    /// Benennt jede der 20 Ein-Buchstaben-Variablen aus <see cref="BaseStatements"/> (a..t) auf
    /// ein distinktes <c>pN</c> um — Regex mit Wortgrenzen statt <see cref="string.Replace"/>-Kette,
    /// damit keine Teilstring-Kollisionen zwischen bereits umbenannten Tokens auftreten (z. B.
    /// wuerde ein naiver sequentieller Replace von "p" nach der Umbenennung von "o" zu "p15" auch
    /// dessen Ziffern treffen).
    /// </summary>
    private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> IdentifierRenameMap =
        "abcdefghijklmnopqrst".ToCharArray()
            .Select((ch, index) => (Letter: ch.ToString(), Renamed: $"p{index + 1}"))
            .ToDictionary(x => x.Letter, x => x.Renamed);

    private static readonly System.Text.RegularExpressions.Regex IdentifierPattern =
        new(@"\b[a-t]\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string RenameIdentifiers(string statement) =>
        IdentifierPattern.Replace(statement, m => IdentifierRenameMap[m.Value]);

    private static string BuildRenamedBody()
    {
        var renamed = BaseStatements.Select(RenameIdentifiers).ToArray();
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

    [Fact]
    public async Task ScanAsync_RenamedIdentifiers_WithoutNormalization_NoCluster()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildRenamedBody()));

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_RenamedIdentifiers_WithNormalization_DetectsAsClone()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildRenamedBody()));

        var options = DefaultOptions with { NormalizeIdentifiers = true };
        var result = await DuplicateDetectionEngine.ScanAsync(solution, options, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
    }

    // ── Konfigurierbare Schwellwerte ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ScanAsync_CustomThresholds_ChangeBucketClassification()
    {
        // Derselbe "near"-Fall (~0.85), aber mit auf 0.99 hochgesetztem ExactThreshold
        // klassifiziert als Near statt Exact bleibt Near — mit auf 0.50 herabgesetztem
        // NearThreshold rutscht derselbe Score dagegen in den Exact-Bucket (>= 0.50 waere sonst
        // "near", aber wir senken stattdessen ExactThreshold unter den beobachteten Score).
        var variant = WithReplacedStatements([8], ["int i = a * 7;"]);
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path,
            ("A.cs", BuildMethod("A", "ComputeBase", BaseStatements)),
            ("B.cs", BuildMethod("B", "ComputeNear", variant)));

        var lenientOptions = DefaultOptions with { ExactThreshold = 0.80 };
        var result = await DuplicateDetectionEngine.ScanAsync(solution, lenientOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(DuplicateSimilarityBucket.Exact, cluster.Bucket);
    }

    [Fact]
    public async Task ScanAsync_EmptySolution_ReturnsNoClusters()
    {
        using var dir = new TempSourceDirectory();
        var solution = CreateAdhocSolution(dir.Path);

        var result = await DuplicateDetectionEngine.ScanAsync(solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(0, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_PathScopeFilter_ExcludesNonMatchingFiles()
    {
        using var dir = new TempSourceDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Included"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Excluded"));
        var solution = CreateAdhocSolution(dir.Path,
            (Path.Combine("Included", "A.cs"), BuildMethod("A", "ComputeOne", BaseStatements)),
            (Path.Combine("Excluded", "B.cs"), BuildMethod("B", "ComputeTwo", BaseStatements)));

        var options = DefaultOptions with { PathScopeFilter = "Included" };
        var result = await DuplicateDetectionEngine.ScanAsync(solution, options, CancellationToken.None);

        Assert.Empty(result.Clusters);
        Assert.Equal(1, result.MethodsScanned);
    }

    // ── Test-Infrastruktur (Pattern uebernommen von DependencyGraphScannerTests) ─────────────

    private static Solution CreateAdhocSolution(string baseDir, params (string FileName, string Content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var runtimeAsm = MetadataReference.CreateFromFile(typeof(System.Runtime.GCLatencyMode).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, runtimeAsm })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), filePath: Path.Combine(baseDir, "Test.slnx"));
        var solution = workspace.AddSolution(solutionInfo).AddProject(projectInfo);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(baseDir, file.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);

            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, Path.GetFileName(fullPath), file.Content, filePath: fullPath);
        }
        return solution;
    }

    private sealed class TempSourceDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ainetlinter-dupdetect-" + Guid.NewGuid().ToString("N"));

        public TempSourceDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
