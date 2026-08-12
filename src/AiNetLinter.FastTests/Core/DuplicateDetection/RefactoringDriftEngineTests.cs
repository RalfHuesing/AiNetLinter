#nullable enable

using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.FastTests.Core.DuplicateDetection;

[Trait("Category", "Component")]
public sealed class RefactoringDriftEngineTests
{
    private static readonly DuplicateDetectionOptions DefaultOptions = new(
        MinTokens: DuplicateDetectionDefaults.MinTokens,
        NgramSize: DuplicateDetectionDefaults.NgramSize,
        MinSharedNgrams: DuplicateDetectionDefaults.MinSharedNgrams,
        ExactThreshold: DuplicateDetectionDefaults.ExactThreshold,
        NearThreshold: DuplicateDetectionDefaults.NearThreshold,
        FuzzyThreshold: DuplicateDetectionDefaults.FuzzyThreshold,
        NormalizeIdentifiers: false);

    private const string StubTypes = """
        public sealed class SerializerOptionsStub { public NamingPolicyStub? PropertyNamingPolicy { get; set; } public bool WriteIndented { get; set; } public bool IgnoreNullValues { get; set; } public int MaxDepth { get; set; } public EncoderStub? Encoder { get; set; } }
        public sealed class NamingPolicyStub { public static NamingPolicyStub CamelCase { get; } = new(); }
        public sealed class EncoderStub { public static EncoderStub Default { get; } = new(); }
        """;

    [Fact]
    public async Task FindSimilarToAsync_IdenticalBodyNotInCallers_ReturnedAsCandidate()
    {
        using var testSolution = CreateSolution(("Stubs.cs", StubTypes), ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")), ("Drifted.cs", BuildHelperBody("Drifted", "BuildInline")));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        var candidate = Assert.Single(result!.Candidates);
        Assert.Contains("BuildInline", candidate.SignatureName, StringComparison.Ordinal);
        Assert.True(candidate.Score >= DefaultOptions.NearThreshold);
    }

    [Fact]
    public async Task FindSimilarToAsync_CandidateListedAsCaller_IsExcluded()
    {
        using var testSolution = CreateSolution(("Stubs.cs", StubTypes), ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")), ("Drifted.cs", BuildHelperBody("Drifted", "BuildInline")));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "BuildDefault");
        var driftedAsCaller = await GetMethodSymbolAsync(testSolution.Solution, "Drifted.cs", "BuildInline");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [driftedAsCaller], DefaultOptions, CancellationToken.None);

        Assert.Empty(result!.Candidates);
    }

    [Fact]
    public async Task FindSimilarToAsync_HelperItself_NeverAppearsAsCandidate()
    {
        using var testSolution = CreateSolution(("Helper.cs", BuildHelperBody("Helper", "BuildDefault")), ("Stubs.cs", StubTypes));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        Assert.DoesNotContain(result!.Candidates, candidate => candidate.SignatureName.Contains("Helper", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindSimilarToAsync_HelperBelowMinTokens_ReturnsNull()
    {
        const string tiny = "public static class TinyHelper { public static int Get() => 1; }";
        using var testSolution = CreateSolution(("A.cs", tiny));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "A.cs", "Get");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindSimilarToAsync_UnrelatedMethod_BelowNearThreshold_NotIncluded()
    {
        const string unrelated = """
            public static class Unrelated { public static int Compute(int x) { int a = x + 1; int b = x - 7; int c = a ^ b; int d = c % 13; if (d > 0) { return d; } return -d; } }
            """;
        using var testSolution = CreateSolution(("Stubs.cs", StubTypes), ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")), ("Unrelated.cs", unrelated));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        Assert.Empty(result!.Candidates);
    }

    [Fact]
    public async Task FindSimilarToAsync_MultipleCandidates_SortedDescendingByScore()
    {
        var nearVariant = (string[])TestHelper.CalibratedBaseStatements.Clone();
        nearVariant[8] = "int i = a * 7;";
        using var testSolution = CreateSolution(
            ("Helper.cs", TestHelper.BuildCalibratedMethod("Helper", "ComputeBase")),
            ("Drifted1.cs", TestHelper.BuildCalibratedMethod("Drifted1", "ComputeExact")),
            ("Drifted2.cs", BuildCalibratedMethod("Drifted2", "ComputeNear", nearVariant)));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "ComputeBase");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        Assert.Equal(2, result!.Candidates.Count);
        Assert.Contains("Drifted1", result.Candidates[0].SignatureName, StringComparison.Ordinal);
        Assert.Equal(1.0, result.Candidates[0].Score, precision: 6);
        Assert.True(result.Candidates[0].Score > result.Candidates[1].Score);
        Assert.InRange(result.Candidates[1].Score, DefaultOptions.NearThreshold, 0.9499);
    }

    [Fact]
    public async Task FindSimilarToAsync_NoOtherMethods_ReturnsEmptyCandidateList()
    {
        using var testSolution = CreateSolution(("Stubs.cs", StubTypes), ("Helper.cs", BuildHelperBody("Helper", "BuildDefault")));
        var helper = await GetMethodSymbolAsync(testSolution.Solution, "Helper.cs", "BuildDefault");

        var result = await DuplicateDetectionEngine.FindSimilarToAsync(testSolution.Solution, helper, [], DefaultOptions, CancellationToken.None);

        Assert.Empty(result!.Candidates);
        Assert.Equal(1, result.MethodsScanned);
    }

    private static string BuildHelperBody(string className, string methodName) => $$"""
        public static class {{className}}
        {
            public static SerializerOptionsStub {{methodName}}()
            {
                var options = new SerializerOptionsStub { PropertyNamingPolicy = NamingPolicyStub.CamelCase, WriteIndented = false, IgnoreNullValues = true, MaxDepth = 32, Encoder = EncoderStub.Default };
                return options;
            }
        }
        """;

    private static RoslynTestSolution CreateSolution(params (string FileName, string Content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\RefactoringDriftEngineTests.slnx",
            new ProjectSpec("RefactoringDriftEngineCases", files));

    private static async Task<IMethodSymbol> GetMethodSymbolAsync(Solution solution, string fileName, string methodName)
    {
        var document = solution.Projects.Single().Documents.Single(candidate => candidate.Name == fileName);
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        var declaration = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.Text == methodName);
        return (IMethodSymbol)semanticModel!.GetDeclaredSymbol(declaration)!;
    }

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
}
