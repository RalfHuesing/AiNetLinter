#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.Tests.Core.DuplicateDetection;

/// <summary>
/// Engine-Ebene-Tests fuer <see cref="DuplicateDetectionEngine.FindSimilarToAsync"/> (Teil C,
/// "absence-of-calls"-Heuristik, siehe <c>tasks/features/07-drift-audit-ideen.md</c> §C). Loest
/// Symbole direkt ueber ein Roslyn-<see cref="SemanticModel"/> auf (statt ueber
/// <c>FindReferencesTool.ResolveSymbolAsync</c>, das ist Gegenstand von
/// <c>RefactoringDriftScannerTests</c>) — diese Ebene prueft ausschliesslich die
/// Fingerprint-Wiederverwendung + Jaccard-basierte Kandidatenfilterung der Engine selbst.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RefactoringDriftEngineTests : IDisposable
{
    private readonly string _tempDir;

    public RefactoringDriftEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ainetlinter-refdrift-engine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    private static readonly DuplicateDetectionOptions DefaultOptions = new(
        MinTokens: DuplicateDetectionDefaults.MinTokens,
        NgramSize: DuplicateDetectionDefaults.NgramSize,
        MinSharedNgrams: DuplicateDetectionDefaults.MinSharedNgrams,
        ExactThreshold: DuplicateDetectionDefaults.ExactThreshold,
        NearThreshold: DuplicateDetectionDefaults.NearThreshold,
        FuzzyThreshold: DuplicateDetectionDefaults.FuzzyThreshold,
        NormalizeIdentifiers: false);

    // Nachgebildet nach McpJsonOptions.Default (mehrere gesetzte Properties auf einem Objekt) —
    // dieselbe Basis wie DuplicateDetectionEngineTests' historischer Regressionstest, hier als
    // Helper-Koerper fuer Teil C.
    private const string StubTypes = """
        public sealed class SerializerOptionsStub
        {
            public NamingPolicyStub? PropertyNamingPolicy { get; set; }
            public bool WriteIndented { get; set; }
            public bool IgnoreNullValues { get; set; }
            public int MaxDepth { get; set; }
            public EncoderStub? Encoder { get; set; }
        }
        public sealed class NamingPolicyStub
        {
            public static NamingPolicyStub CamelCase { get; } = new();
        }
        public sealed class EncoderStub
        {
            public static EncoderStub Default { get; } = new();
        }
        """;

    private static string BuildHelperBody(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static SerializerOptionsStub {{methodName}}()
            {
                var options = new SerializerOptionsStub
                {
                    PropertyNamingPolicy = NamingPolicyStub.CamelCase,
                    WriteIndented = false,
                    IgnoreNullValues = true,
                    MaxDepth = 32,
                    Encoder = EncoderStub.Default,
                };
                return options;
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
            File.WriteAllText(fullPath, file.Content);
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Content, filePath: fullPath);
        }
        return solution;
    }

    private static async Task<IMethodSymbol> GetMethodSymbolAsync(Solution solution, string fileName, string methodName)
    {
        var document = solution.Projects.Single().Documents.Single(d => d.Name == fileName);
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        var declaration = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == methodName);
        return (IMethodSymbol)semanticModel!.GetDeclaredSymbol(declaration)!;
    }

    [Fact]
    public async Task FindSimilarToAsync_IdenticalBodyNotInCallers_ReturnedAsCandidate()
    {
        var solution = CreateAdhocSolution(
            ("Stubs.cs", StubTypes),
            ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")),
            ("Drifted.cs", BuildHelperBody("Drifted", "BuildInline")));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        var candidate = Assert.Single(result!.Candidates);
        Assert.Contains("BuildInline", candidate.SignatureName, StringComparison.Ordinal);
        Assert.True(candidate.Score >= DefaultOptions.NearThreshold);
    }

    [Fact]
    public async Task FindSimilarToAsync_CandidateListedAsCaller_IsExcluded()
    {
        var solution = CreateAdhocSolution(
            ("Stubs.cs", StubTypes),
            ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")),
            ("Drifted.cs", BuildHelperBody("Drifted", "BuildInline")));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "BuildDefault");
        var driftedAsCaller = await GetMethodSymbolAsync(solution, "Drifted.cs", "BuildInline");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, new ISymbol[] { driftedAsCaller }, DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    [Fact]
    public async Task FindSimilarToAsync_HelperItself_NeverAppearsAsCandidate()
    {
        var solution = CreateAdhocSolution(("Helper.cs", BuildHelperBody("Helper", "BuildDefault")), ("Stubs.cs", StubTypes));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Candidates, c => c.SignatureName.Contains("Helper", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindSimilarToAsync_HelperBelowMinTokens_ReturnsNull()
    {
        const string tiny = """
            public static class TinyHelper
            {
                public static int Get() => 1;
            }
            """;
        var solution = CreateAdhocSolution(("A.cs", tiny));
        var helper = await GetMethodSymbolAsync(solution, "A.cs", "Get");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindSimilarToAsync_UnrelatedMethod_BelowNearThreshold_NotIncluded()
    {
        const string unrelated = """
            public static class Unrelated
            {
                public static int Compute(int x)
                {
                    int a = x + 1; int b = x - 7; int c = a ^ b; int d = c % 13;
                    if (d > 0) { return d; }
                    return -d;
                }
            }
            """;
        var solution = CreateAdhocSolution(
            ("Stubs.cs", StubTypes),
            ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")),
            ("Unrelated.cs", unrelated));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
    }

    // 20 unabhaengige "int X = expr;"-Statements — identische Basis wie
    // DuplicateDetectionEngineTests, dort per Testlauf kalibriert: ein einzelner ersetzter
    // Statement-Swap landet verlaesslich im near-Bucket (~0.85), sicher innerhalb
    // [NearThreshold, ExactThreshold). Hier wiederverwendet, um die Score-Sortierung ohne neue,
    // unkalibrierte Score-Annahmen zu testen.
    private static readonly string[] CalibratedBaseStatements =
    [
        "int a = x + 1;", "int b = x + 2;", "int c = x + 3;", "int d = x + 4;", "int e = x + 5;",
        "int f = a + b;", "int g = c + d;", "int h = e + f;", "int i = g + h;", "int j = i - a;",
        "int k = j - b;", "int l = k - c;", "int m = l - d;", "int n = m - e;", "int o = n * 2;",
        "int p = o / 2;", "int q = p + 1;", "int r = q + 2;", "int s = r + 3;", "int t = s + 4;",
    ];

    private static string BuildCalibratedMethod(string className, string methodName, IReadOnlyList<string> statements)
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

    [Fact]
    public async Task FindSimilarToAsync_MultipleCandidates_SortedDescendingByScore()
    {
        // Drifted1 ist byte-identisch zum Helper (Score 1.0), Drifted2 hat ein einzelnes ersetztes
        // Statement (kalibriert auf ~0.85, sicher >= NearThreshold aber < 1.0) -> Reihenfolge muss
        // Drifted1 vor Drifted2 zeigen.
        var nearVariant = (string[])CalibratedBaseStatements.Clone();
        nearVariant[8] = "int i = a * 7;";

        var solution = CreateAdhocSolution(
            ("Helper.cs", BuildCalibratedMethod("Helper", "ComputeBase", CalibratedBaseStatements)),
            ("Drifted1.cs", BuildCalibratedMethod("Drifted1", "ComputeExact", CalibratedBaseStatements)),
            ("Drifted2.cs", BuildCalibratedMethod("Drifted2", "ComputeNear", nearVariant)));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "ComputeBase");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Candidates.Count);
        Assert.Contains("Drifted1", result.Candidates[0].SignatureName, StringComparison.Ordinal);
        Assert.Equal(1.0, result.Candidates[0].Score, precision: 6);
        Assert.True(result.Candidates[0].Score > result.Candidates[1].Score);
        Assert.InRange(result.Candidates[1].Score, DefaultOptions.NearThreshold, 0.9499);
    }

    [Fact]
    public async Task FindSimilarToAsync_NoOtherMethods_ReturnsEmptyCandidateList()
    {
        var solution = CreateAdhocSolution(("Stubs.cs", StubTypes), ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")));
        var helper = await GetMethodSymbolAsync(solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(
            solution, helper, Array.Empty<ISymbol>(), DefaultOptions, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Candidates);
        Assert.Equal(1, result.MethodsScanned);
    }
}
