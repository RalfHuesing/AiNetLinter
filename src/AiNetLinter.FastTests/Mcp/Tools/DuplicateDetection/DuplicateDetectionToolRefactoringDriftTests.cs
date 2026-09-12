#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.DuplicateDetection;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Tests fuer <see cref="DuplicateDetectionTool"/>s <c>mode="refactoring-drift"</c>-Dispatch (Teil
/// C) — Mode-Parsing, <c>helperSymbol</c>-Pflicht, Fehler-Durchreichung von
/// <see cref="RefactoringDriftScanner"/> und fachliche Kandidatenausgabe.
/// </summary>
[Trait("Category", "Component")]
public sealed class DuplicateDetectionToolRefactoringDriftTests
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

    private static McpInMemoryTestContext CreateContext(params (string FileName, string Content)[] files) =>
        new(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionToolRefactoringDriftTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: ".")));

    [Fact]
    public async Task ExecuteAsync_UnknownMode_ReturnsInvalidArgumentListingValidModes()
    {
        using var context = CreateContext(("A.cs", Helper), ("Stubs.cs", StubTypes));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, "sideways", null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("clone", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("refactoring-drift", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("gitRef", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDriftModeWithoutHelperSymbol_ReturnsInvalidArgument()
    {
        using var context = CreateContext(("A.cs", Helper), ("Stubs.cs", StubTypes));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("helperSymbol", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("gitRef", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorEvenForRefactoringDriftMode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "Foo.Bar"), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDrift_UnresolvableHelperSymbol_PassesThroughSymbolNotFoundError()
    {
        using var context = CreateContext(("A.cs", Helper), ("Stubs.cs", StubTypes));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "DoesNotExistXyz"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDrift_FindsCandidate_TextSaysCandidatesNotViolations()
    {
        using var context = CreateContext(("Stubs.cs", StubTypes), ("Helper.cs", Helper), ("DriftedA.cs", DriftedA));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state,
            new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Kandidat", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Verstoss", textContent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Verstoß", textContent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DriftedA", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDrift_ReportsHelperAndCandidateInContent()
    {
        using var context = CreateContext(("Stubs.cs", StubTypes), ("Helper.cs", Helper), ("DriftedA.cs", DriftedA));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state,
            new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault"),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("BuildDefault", text, StringComparison.Ordinal);
        Assert.Contains("DriftedA", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDrift_CandidateFound_EvidenzgrenzeAppearsOnlyOnceInHeader()
    {
        using var context = CreateContext(("Stubs.cs", StubTypes), ("Helper.cs", Helper), ("DriftedA.cs", DriftedA));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state,
            new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault"),
            CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(text, "Evidenzgrenze").Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(text, "Countercheck").Count);
    }

    [Fact]
    public async Task ExecuteAsync_RefactoringDrift_NoCandidates_TextSaysNoCandidatesFound()
    {
        using var context = CreateContext(("Stubs.cs", StubTypes), ("Helper.cs", Helper));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state,
            new DuplicateDetectionInput(null, null, null, null, null, "refactoring-drift", "OptionsHelper.BuildDefault"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Refactoring-Drift-Kandidaten", textContent.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("clone")]
    public async Task ExecuteAsync_DefaultAndCloneMode_ReturnClusters(string? mode)
    {
        using var context = CreateContext(("A.cs", Helper), ("B.cs", Helper.Replace("OptionsHelper", "OtherHelper")), ("Stubs.cs", StubTypes));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, mode, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Duplikat-Kandidatencluster", text, StringComparison.Ordinal);
    }
}
