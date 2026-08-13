#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="SafeguardTool"/>. Pattern 1:1 von <see cref="GetViolationsToolTests"/>:
/// <c>[Collection("SymbolGraphCatalog")]</c>, ein eigener <see cref="McpCodeGraphServer"/>
/// je Test, Test-Naming <c>ExecuteAsync_&lt;Bedingung&gt;_&lt;Erwartung&gt;</c>. Zusaetzlicher
/// Fokus: <c>passed=false</c> ist explizit NICHT <c>isError=true</c> (Anti-Pattern-Falle aus
/// <c>IsErrorPolicy.md</c> und Konzept §"Zielplattformen") — der entsprechende Test ist als
/// Regressionsschutz fuer genau diese Falle benannt.
/// </summary>
[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class SafeguardToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public SafeguardToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReturnsCallToolResultWithScore()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.False(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Safeguard-Score", textContent.Text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);

        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.NotNull(json["score"]);
        Assert.NotNull(json["threshold"]);
        Assert.NotNull(json["passed"]);
        Assert.NotNull(json["violations"]);
        Assert.NotNull(json["remediation"]);
        Assert.NotNull(json["summary"]);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_FailedScore_PassedFalseButIsErrorFalse()
    {
        // Regressionstest fuer die Anti-Pattern-Falle aus IsErrorPolicy.md / Konzept
        // §"Zielplattformen": ein Score mit Passed=false (z. B. minScore=100.0 — kein
        // realer Score erreicht diesen Wert) ist explizit KEIN isError=true, sondern der
        // erwartete Output des Quality-Gate-Tools. Beide Flags muessen getrennt geprueft
        // werden, damit eine spaetere Refactoring-Welle, die das koppelt, sofort auffliegt.
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 100.0, 20, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.Equal(false, (bool)json["passed"]!);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("FAIL", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilter_PassesToScanner()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await SafeguardTool.ExecuteAsync(state, "SymbolGraphMini", 8.0, 20, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        var violations = (JsonArray)json["violations"]!;
        // Bei einem Treffer im Scope muss mindestens eine Violation den Fixture-Projektnamen tragen.
        Assert.NotEmpty(violations);
    }

    [Fact]
    public async Task ExecuteAsync_MinScoreAndMaxViolationsOverrides_AreHonored()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 0.0, 1, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = JsonSerializer.Deserialize<JsonObject>(result.StructuredContent!.Value.GetRawText())!;
        Assert.NotNull(json["threshold"]);
        Assert.Equal(0.0, (double)json["threshold"]!);
        var violations = (JsonArray)json["violations"]!;
        Assert.True(violations.Count <= 1, $"maxViolations=1 erzwingt <= 1 Eintrag, gefunden: {violations.Count}");
    }

    [Fact]
    public async Task ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint()
    {
        // Regressionstest analog GetViolationsToolTests: ThrowingTextLoader simuliert eine
        // echte LinterEngine-Malfunction deterministisch (statt auf einen fragilen realen
        // Timing-Race zu warten). Erwartet: IsError=true, ANALYSIS_FAILED-Code, Retry-Hinweis
        // und die rohe Exception-Message im Text.
        var probeDir = Path.Combine(Path.GetTempPath(), "ainetlinter-safeguard-tool-malfunction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeDir);
        try
        {
            var solution = TestHelper.CreateFaultySolution(probeDir);

            var catalog = new AiNetLinter.Baseline.SourceFileCatalog(solution, hasLoadingErrors: false);
            using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

            var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Null(result.StructuredContent);
            var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Contains("ANALYSIS_FAILED", textContent.Text, StringComparison.Ordinal);
            Assert.Contains("Einmal erneut versuchen", textContent.Text, StringComparison.Ordinal);
            Assert.Contains("Simulierter Lesefehler", textContent.Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(probeDir, recursive: true);
        }
    }
}
