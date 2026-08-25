#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DuplicateDetection;

[Trait("Category", "Component")]
public sealed class RefactoringDriftScannerTests
{
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

    private const string Helper = """
        public static class OptionsHelper
        {
            public static SerializerOptionsStub BuildDefault()
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

    private const string GoodCallerA = """
        public static class GoodCallerA
        {
            public static SerializerOptionsStub Get() => OptionsHelper.BuildDefault();
        }
        """;

    private const string GoodCallerB = """
        public static class GoodCallerB
        {
            public static SerializerOptionsStub Get() => OptionsHelper.BuildDefault();
        }
        """;

    private const string DriftedA = """
        public static class DriftedA
        {
            public static SerializerOptionsStub Build()
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

    private const string DriftedB = """
        public static class DriftedB
        {
            public static SerializerOptionsStub Build()
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

    private const string LambdaCaller = """
        public static class LambdaCaller
        {
            public static SerializerOptionsStub Build()
            {
                System.Func<SerializerOptionsStub> helper = () => OptionsHelper.BuildDefault();
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

    [Fact]
    public async Task ScanAsync_HistoricalRegression_FindsInlineDuplicatesButNotCorrectCallersOrHelperItself()
    {
        using var testSolution = CreateFullSolution();

        var (result, error) = await ScanAsync(testSolution);

        Assert.Null(error);
        Assert.NotNull(result);
        var names = result!.ShownCandidates.Select(candidate => candidate.SignatureName).ToList();
        Assert.Equal(2, result.ShownCandidates.Count);
        Assert.Contains(names, name => name.Contains("DriftedA", System.StringComparison.Ordinal));
        Assert.Contains(names, name => name.Contains("DriftedB", System.StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("GoodCallerA", System.StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("GoodCallerB", System.StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("OptionsHelper", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScanAsync_UnknownHelperSymbol_ReturnsSymbolNotFoundError()
    {
        using var testSolution = CreateFullSolution();

        var (result, error) = await ScanAsync(testSolution, "DoesNotExistXyz");

        Assert.Null(result);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_HelperSymbolResolvesToProperty_ReturnsInvalidArgumentAboutMethodRequirement()
    {
        const string typeWithProperty = """
            public static class HasProperty
            {
                public static int Value { get; set; } = 42;
            }
            """;
        using var testSolution = CreateSolution(("A.cs", typeWithProperty));

        var (result, error) = await ScanAsync(testSolution, "HasProperty.Value");

        Assert.Null(result);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("gewoehnliche Methode", textContent.Text, System.StringComparison.Ordinal);
        Assert.NotEqual(true, error.IsError);
    }

    [Fact]
    public async Task ScanAsync_HelperTooShortForMinTokens_ReturnsInvalidArgumentAboutFingerprintRequirements()
    {
        const string tiny = """
            public static class TinyHelper
            {
                public static int Get() => 1;
            }
            public static class Caller
            {
                public static int Use() => TinyHelper.Get();
            }
            """;
        using var testSolution = CreateSolution(("A.cs", tiny));

        var (result, error) = await ScanAsync(testSolution, "TinyHelper.Get");

        Assert.Null(result);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("minTokens=30", textContent.Text, System.StringComparison.Ordinal);
        Assert.Contains("Body-Token", textContent.Text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("ausgeschlossenes Verzeichnis", textContent.Text, System.StringComparison.Ordinal);
        Assert.NotEqual(true, error.IsError);
    }

    [Fact]
    public async Task ScanAsync_MaxResultsFromInput_TruncatesCandidates()
    {
        using var testSolution = CreateFullSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, MaxResults: 1, "refactoring-drift", "OptionsHelper.BuildDefault");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(
            testSolution.Solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Single(result!.ShownCandidates);
        Assert.Equal(2, result.TotalCandidates);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanAsync_HelperSymbolDisplayName_IsPopulatedInResult()
    {
        using var testSolution = CreateFullSolution();

        var (result, error) = await ScanAsync(testSolution);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Contains("BuildDefault", result!.HelperSymbolDisplayName, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_NoInlineDuplicates_ReturnsEmptyCandidateList()
    {
        using var testSolution = CreateSolution(
            ("Stubs.cs", StubTypes), ("Helper.cs", Helper),
            ("GoodCallerA.cs", GoodCallerA), ("GoodCallerB.cs", GoodCallerB));

        var (result, error) = await ScanAsync(testSolution);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Empty(result!.ShownCandidates);
    }

    [Fact]
    public async Task ScanAsync_LambdaHelperCaller_IsExcludedWhileInlineCandidateRemainsVisible()
    {
        using var testSolution = CreateSolution(
            ("Stubs.cs", StubTypes), ("Helper.cs", Helper),
            ("LambdaCaller.cs", LambdaCaller), ("DriftedA.cs", DriftedA));

        var (result, error) = await ScanAsync(testSolution);

        Assert.Null(error);
        Assert.NotNull(result);
        var names = result!.ShownCandidates.Select(candidate => candidate.SignatureName).ToList();
        Assert.Contains(names, name => name.Contains("DriftedA", System.StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("LambdaCaller", System.StringComparison.Ordinal));
    }

    private static RoslynTestSolution CreateFullSolution() => CreateSolution(
        ("Stubs.cs", StubTypes), ("Helper.cs", Helper),
        ("GoodCallerA.cs", GoodCallerA), ("GoodCallerB.cs", GoodCallerB),
        ("DriftedA.cs", DriftedA), ("DriftedB.cs", DriftedB));

    private static RoslynTestSolution CreateSolution(params (string FileName, string Content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\RefactoringDriftScannerTests.slnx",
            new ProjectSpec("RefactoringDriftCases", files));

    private static Task<(RefactoringDriftScanResultForTool? Result, CallToolResult? Error)> ScanAsync(
        RoslynTestSolution testSolution,
        string helperSymbol = "OptionsHelper.BuildDefault")
    {
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", helperSymbol);
        return RefactoringDriftScanner.ScanAsync(testSolution.Solution, new GlobalConfig(), input, CancellationToken.None);
    }
}
