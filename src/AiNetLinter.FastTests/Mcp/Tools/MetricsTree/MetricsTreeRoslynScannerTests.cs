#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.MetricsTree;

/// <summary>
/// Tests fuer die zwei Roslyn-Modi <c>violation_density</c>/<c>complexity</c> von
/// <c>metrics_tree</c> — ueber <see cref="MetricsTreeTool.ExecuteAsync"/>, analog
/// <see cref="MetricsTreeToolTests"/> fuer die Datei-Modi. Nutzt fuer <c>violation_density</c> die
/// geteilte <see cref="McpInMemoryTestContext"/> (ViolationTrigger.cs traegt bereits einen
/// bekannten Verstoss). Fuer <c>complexity</c> braucht es zusaetzlich eine verzweigungsreiche
/// Methode bzw. eine methodenlose Datei — beides fehlt im geteilten SymbolGraphMini-Fixture (dessen
/// exakte Datei-/Methodenzahl von anderen Tests, z. B. GetIndexScopeToolTests, exakt geprueft wird)
/// — dafuer eigene, isolierte <see cref="SymbolGraphMiniFixtureWorkspace"/>-Instanzen mit zusaetzlich
/// geschriebenen Dateien (Pattern analog
/// <c>GetIndexScopeToolTests.ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromXamlHtmlCount</c>).
/// </summary>
[Trait("Category", "Component")]
public sealed class MetricsTreeRoslynScannerTests
{
    private readonly McpInMemoryTestContext _fixture;

    public MetricsTreeRoslynScannerTests() { _fixture = new McpInMemoryTestContext(); }

    private McpCodeGraphServer NewState() =>
        _fixture.CreateServer();

    [Fact]
    public async Task ExecuteAsync_ViolationDensityMode_ReturnsTreeSortedByViolationCountDescending()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini", "violation_density", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        // Alle 5 Fixture-Dateien triggern EnforceSealedClasses (Default-Regel, nicht-sealed public
        // Klassen) — Hierarchy.cs traegt zwei Klassen (BaseGreeting, SpecialGreeting) und damit 2
        // Violations, alle anderen Dateien je 1. Bei absteigender Sortierung nach Violation-Count
        // muss Hierarchy.cs zuerst erscheinen.
        Assert.Contains("Hierarchy.cs", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Hierarchy.cs", StringComparison.Ordinal) <
                     text.IndexOf("Greeter.cs", StringComparison.Ordinal), text);
        Assert.Contains("2 Violations", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ViolationDensityMode_RootNotMatchingAnyFile_ReturnsExplicitEmptyMessage()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("DoesNotExistAnywhere", "violation_density", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Keine Dateien unter root=", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ViolationDensityMode_MaxDepth_DoesNotThrowAndClampsGracefully()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs(null, "violation_density", 5, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("ViolationTrigger.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexityMode_RootPointingToSingleFile_ReturnsSingleNodeTree()
    {
        var state = NewState();

        var result = await MetricsTreeTool.ExecuteAsync(
            state, new MetricsTreeToolArgs("src/SymbolGraphMini/Greeter.cs", "complexity", 1, 10, null), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.Contains("Ø CC", text, StringComparison.Ordinal);
        Assert.DoesNotContain("├──", text, StringComparison.Ordinal);
        Assert.DoesNotContain("└──", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexityMode_HighComplexityMethodVsTrivialMethod_SortsHighComplexityFirst()
    {
        // Isolierte Workspace-Kopie (nicht die geteilte _fixture), weil eine zusaetzliche Datei mit
        // hoher zyklomatischer Komplexitaet die von GetIndexScopeToolTests/GetHotspotsToolTests
        // exakt geprueften Datei-/Methodenzahlen des geteilten SymbolGraphMini-Fixtures veraendern
        // wuerde.
        var scenario = CreateComplexityScenario("HighComplexity.cs", """
            namespace SymbolGraphMini;

            public class HighComplexity
            {
                public int Branchy(int x)
                {
                    if (x == 1) { x++; }
                    else if (x == 2) { x += 2; }
                    else if (x == 3) { x += 3; }
                    else { x += 4; }

                    for (var i = 0; i < x; i++)
                    {
                        if (i % 2 == 0) { x += i; }
                    }

                    switch (x)
                    {
                        case 1: x++; break;
                        case 2: x += 2; break;
                        default: x += 3; break;
                    }

                    return x;
                }
            }
            """);

        using var context = new McpInMemoryTestContext(scenario);
        using var state = context.CreateServer();

        var result = await MetricsTreeTool.ExecuteAsync(
            state,
            new MetricsTreeToolArgs("src/SymbolGraphMini", "complexity", 1, 10, "HighComplexity|Greeter"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("HighComplexity.cs", text, StringComparison.Ordinal);
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        // Branchy() hat deutlich hoehere zyklomatische Komplexitaet als Greeter.Greet()
        // (Expression-Body, CC 1) — bei absteigender Sortierung muss HighComplexity.cs zuerst
        // erscheinen.
        Assert.True(text.IndexOf("HighComplexity.cs", StringComparison.Ordinal) <
                     text.IndexOf("Greeter.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ComplexityMode_FileWithoutMethods_ReturnsZeroMetricsWithoutCrash()
    {
        var scenario = CreateComplexityScenario("NoMethods.cs", """
            namespace SymbolGraphMini;

            public class NoMethods
            {
                public int Value;
            }
            """);

        using var context = new McpInMemoryTestContext(scenario);
        using var state = context.CreateServer();

        var result = await MetricsTreeTool.ExecuteAsync(
            state,
            new MetricsTreeToolArgs("src/SymbolGraphMini", "complexity", 1, 10, "NoMethods"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("NoMethods.cs", text, StringComparison.Ordinal);
        Assert.Contains("Ø CC 0.0", text, StringComparison.Ordinal);
        Assert.Contains("max CC 0", text, StringComparison.Ordinal);
    }

    private static RoslynTestSolution CreateComplexityScenario(string fileName, string content) =>
        McpInMemoryTestContext.CreateScenario(new ProjectSpec("src", [
            ("SymbolGraphMini/Greeter.cs", "namespace SymbolGraphMini; public class Greeter { public string Greet(string name) => name; }"),
            ($"SymbolGraphMini/{fileName}", content)
        ]));
}
