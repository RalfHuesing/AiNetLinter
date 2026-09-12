#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.FastTests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Safeguard;

/// <summary>
/// Tests fuer <see cref="SafeguardTool"/>. Jeder Test verwendet einen eigenen
/// <see cref="McpCodeGraphServer"/> und folgt dem Naming
/// <c>ExecuteAsync_&lt;Bedingung&gt;_&lt;Erwartung&gt;</c>. Zusaetzlicher
/// Fokus: <c>passed=false</c> ist explizit NICHT <c>isError=true</c> (Anti-Pattern-Falle aus
/// <c>IsErrorPolicy.md</c>) — der entsprechende Test ist als Regressionsschutz fuer genau
/// diese Falle benannt.
/// </summary>
[Trait("Category", "Component")]
public sealed class SafeguardToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public SafeguardToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Solution ist nicht geladen", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Server-Log", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ReturnsCallToolResultWithScore()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Safeguard-Score", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Threshold 8,00", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("scoreIsNotScope=true", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_ListsTopViolationDetailsInContent()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Top-Befunde:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Problem:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Datei:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Zeile:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Regel:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Severity:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Guidance:", textContent.Text, StringComparison.Ordinal);

    }

    [Fact]
    public async Task ExecuteAsync_MaxViolations_ReportsTotalAndTruncationMetadata()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 0, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Top-Auswahl wegen maxViolations", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("get_violations aufrufen", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolution_FailedScore_PassedFalseButIsErrorFalse()
    {
        // Regressionstest fuer die Anti-Pattern-Falle aus IsErrorPolicy.md: ein Score mit
        // Passed=false (z. B. minScore=100.0 — kein
        // realer Score erreicht diesen Wert) ist explizit KEIN isError=true, sondern der
        // erwartete Output des Quality-Gate-Tools. Beide Flags muessen getrennt geprueft
        // werden, damit eine spaetere Refactoring-Welle, die das koppelt, sofort auffliegt.
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, null, 100.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("FAIL", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MissingConfig_ReturnsNotConfiguredWithoutScore()
    {
        using var context = new McpInMemoryTestContext();
        using var state = context.CreateServer(includeConfig: false);

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("NOT_CONFIGURED", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilter_PassesToScanner()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, "SymbolGraphMini", 8.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Top-Befunde:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardSlashScopeFilter_MatchesClasses()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, "src/SymbolGraphMini", 8.0, 20, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Top-Befunde:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MinScoreAndMaxViolationsOverrides_AreHonored()
    {
        var state = _fixture.CreateServer();

        var result = await SafeguardTool.ExecuteAsync(state, null, 0.0, 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Threshold 0,00", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Befund #2", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint()
    {
        // Regressionstest analog GetViolationsToolTests: ThrowingTextLoader simuliert eine
        // echte LinterEngine-Malfunction deterministisch (statt auf einen fragilen realen
        // Timing-Race zu warten). Erwartet: IsError=true, ANALYSIS_FAILED-Code, Retry-Hinweis
        // und die rohe Exception-Message im Text.
        using var faulty = new FaultingSolutionFixture();
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, Config: TestHelper.CreateDefaultConfig(), ReadOnlySolutionSnapshot: faulty.Solution)));

        var result = await SafeguardTool.ExecuteAsync(state, null, 8.0, 20, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ANALYSIS_FAILED", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Safeguard-Berechnung", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Einmal erneut versuchen", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Simulierter Lesefehler", textContent.Text, StringComparison.Ordinal);
    }
}
