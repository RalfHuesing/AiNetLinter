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
/// Component-Tests fuer find_duplicates mode=structural:
/// Mode-Dispatch, Argument-Validierung und fachlichen Text-Output,
/// Rueckwaertskompatibilitaet der vorhandenen Modi.
/// </summary>
[Trait("Category", "Component")]
public sealed class DuplicateDetectionToolStructuralTests
{
    private static McpInMemoryTestContext CreateContext(params (string FileName, string Content)[] files) =>
        new(RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DuplicateDetectionToolStructuralTests.slnx",
            new ProjectSpec("TestProject", files, VirtualProjectDirectory: ".")));

    private static string BuildMapper(string className, string methodName, string returnValue) => $$"""
        using Microsoft.CodeAnalysis;
        public static class {{className}}
        {
            public static string {{methodName}}(ITypeSymbol symbol) =>
                symbol.TypeKind switch
                {
                    TypeKind.Class     => "{{returnValue}}-class",
                    TypeKind.Interface => "{{returnValue}}-interface",
                    TypeKind.Struct    => "{{returnValue}}-struct",
                    TypeKind.Enum      => "{{returnValue}}-enum",
                    _                  => "{{returnValue}}-other",
                };
        }
        """;

    // ── Mode-Parsing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_InvalidMode_ReturnsRecoverableInvalidArgument()
    {
        using var context = CreateContext(("A.cs", BuildMapper("A", "MapA", "a")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "unknown-mode"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("structural", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StructuralModeEmpty_ReturnsKandidatenText()
    {
        using var context = CreateContext(("A.cs", BuildMapper("A", "MapA", "a")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "structural"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Kandidaten", textContent.Text, StringComparison.OrdinalIgnoreCase);
    }

    // ── Structural-Output ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StructuralMode_ReportsStructuralCandidatesInContent()
    {
        using var context = CreateContext(
            ("A.cs", BuildMapper("A", "MapA", "a")),
            ("B.cs", BuildMapper("B", "MapB", "b")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "structural"), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("strukturell", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kandidaten", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_StructuralMode_SummaryContainsMethodsScannedAndMode()
    {
        using var context = CreateContext(
            ("A.cs", BuildMapper("A", "MapA", "a")),
            ("B.cs", BuildMapper("B", "MapB", "b")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "structural"), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Methoden gescannt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StructuralMode_ClusterMemberHasStructureProfile()
    {
        using var context = CreateContext(
            ("A.cs", BuildMapper("A", "MapA", "a")),
            ("B.cs", BuildMapper("B", "MapB", "b")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(MinTokens: 1, null, null, null, null, Mode: "structural"), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Profil:", text, StringComparison.Ordinal);
    }

    // ── Rueckwaertskompatibilitaet ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CloneMode_StillFindsExactDuplicates()
    {
        using var context = CreateContext(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
            ("B.cs", TestHelper.BuildCalibratedMethod("B", "Two")));
        var state = context.CreateServer();

        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "clone"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("exact", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultMode_BehavesLikeCloneMode()
    {
        using var context = CreateContext(
            ("A.cs", TestHelper.BuildCalibratedMethod("A", "One")),
            ("B.cs", TestHelper.BuildCalibratedMethod("B", "Two")));
        var state = context.CreateServer();

        var defaultResult = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null), CancellationToken.None);
        var cloneResult = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(null, null, null, null, null, Mode: "clone"), CancellationToken.None);

        var defaultText = Assert.IsType<TextContentBlock>(Assert.Single(defaultResult.Content)).Text;
        var cloneText = Assert.IsType<TextContentBlock>(Assert.Single(cloneResult.Content)).Text;
        Assert.Contains("exact", defaultText, StringComparison.Ordinal);
        Assert.Contains("exact", cloneText, StringComparison.Ordinal);
    }

    // ── StructuralDuplicateScanner.BuildOptions (minTokens-Durchreiche) ──────────────────────────

    [Fact]
    public async Task ExecuteAsync_StructuralMode_MinTokensIsRespected()
    {
        using var context = CreateContext(
            ("A.cs", BuildMapper("A", "MapA", "a")),
            ("B.cs", BuildMapper("B", "MapB", "b")));
        var state = context.CreateServer();

        // Mit sehr hohem minTokens-Filter: kein Ergebnis erwartet
        var result = await DuplicateDetectionTool.ExecuteAsync(
            state, new DuplicateDetectionInput(MinTokens: 10000, null, null, null, null, Mode: "structural"), CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("0 Methoden gescannt", text, StringComparison.Ordinal);
    }
}
