#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="RefactoringDriftScanner"/> (Teil C) — Symbol-Aufloesung ueber
/// <c>FindReferencesTool.ResolveSymbolAsync</c> (wiederverwendet), Aufrufer-Aufloesung ueber
/// <c>DiffImpactAnalyzer.FindCallSiteEntriesAsync</c> + Positions-Resolution auf die umschliessende
/// Methode, und die eigentliche Kandidatensuche via
/// <see cref="Core.DuplicateDetection.DuplicateDetectionEngine.FindSimilarToAsync"/>. Nutzt dieselbe
/// "Optionen-Objekt mit mehreren Properties"-Fixture wie
/// <c>tasks/features/05-roadmap.md</c> "Teil C" fordert (nachgebildet nach <c>McpJsonOptions.Default</c>).
/// </summary>
[Trait("Category", "Unit")]
public sealed class RefactoringDriftScannerTests : IDisposable
{
    private readonly string _tempDir;

    public RefactoringDriftScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ainetlinter-refdrift-scanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // Nachgebildet nach McpJsonOptions.Default (mehrere gesetzte Properties auf einem Objekt),
    // siehe src/AiNetLinter/Mcp/McpJsonOptions.cs als reales Vorbild.
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

    // (b) korrekte Aufrufer — MUESSEN nicht als Kandidaten erscheinen.
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

    // (c) inline-duplizierter Code OHNE Aufruf des Helpers — MUESSEN als Kandidaten erscheinen.
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

    private Solution CreateFullSolution()
    {
        return CreateAdhocSolution(
            ("Stubs.cs", StubTypes), ("Helper.cs", Helper),
            ("GoodCallerA.cs", GoodCallerA), ("GoodCallerB.cs", GoodCallerB),
            ("DriftedA.cs", DriftedA), ("DriftedB.cs", DriftedB));
    }

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

    [Fact]
    public async Task ScanAsync_HistoricalRegression_FindsInlineDuplicatesButNotCorrectCallersOrHelperItself()
    {
        var solution = CreateFullSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        var names = result!.ShownCandidates.Select(c => c.SignatureName).ToList();
        Assert.Equal(2, result.ShownCandidates.Count);
        Assert.Contains(names, n => n.Contains("DriftedA", StringComparison.Ordinal));
        Assert.Contains(names, n => n.Contains("DriftedB", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("GoodCallerA", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("GoodCallerB", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("OptionsHelper", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScanAsync_UnknownHelperSymbol_ReturnsSymbolNotFoundError()
    {
        var solution = CreateFullSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "DoesNotExistXyz");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
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
        var solution = CreateAdhocSolution(("A.cs", typeWithProperty));
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "HasProperty.Value");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("gewoehnliche Methode", textContent.Text, StringComparison.Ordinal);
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
        var solution = CreateAdhocSolution(("A.cs", tiny));
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "TinyHelper.Get");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(result);
        Assert.NotNull(error);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(error!.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.NotEqual(true, error.IsError);
    }

    [Fact]
    public async Task ScanAsync_MaxResultsFromInput_TruncatesCandidates()
    {
        var solution = CreateFullSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, MaxResults: 1, "refactoring-drift", "OptionsHelper.BuildDefault");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Single(result!.ShownCandidates);
        Assert.Equal(2, result.TotalCandidates);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ScanAsync_HelperSymbolDisplayName_IsPopulatedInResult()
    {
        var solution = CreateFullSolution();
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Contains("BuildDefault", result!.HelperSymbolDisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAsync_NoInlineDuplicates_ReturnsEmptyCandidateList()
    {
        var solution = CreateAdhocSolution(
            ("Stubs.cs", StubTypes), ("Helper.cs", Helper),
            ("GoodCallerA.cs", GoodCallerA), ("GoodCallerB.cs", GoodCallerB));
        var input = new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault");

        var (result, error) = await RefactoringDriftScanner.ScanAsync(solution, new GlobalConfig(), input, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Empty(result!.ShownCandidates);
    }
}
