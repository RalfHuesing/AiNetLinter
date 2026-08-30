#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer Namespace-, Metrik- und Feedback-Ergebnisse.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerToolContractE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerToolContractE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetNamespaceTree_NoArguments_ReturnsSolutionOverview()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>());

        Assert.Contains("# Solution Overview", text, StringComparison.Ordinal);
        Assert.Contains("SymbolGraphMini", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNamespaceTree_SpecificProject_ReturnsNamespaces()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["project"] = "SymbolGraphMini",
                ["includeTypes"] = false
            });

        Assert.Contains("# Namespaces in Projekt 'SymbolGraphMini'", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetricsLookup_ValidMethod_ReturnsMetricsText()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "metrics_lookup",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = new[] { "Greeter.Greet" } });

        Assert.Contains("Greet", text, StringComparison.Ordinal);
        Assert.Contains("Schwellwert-Abgleich", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetricsLookup_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "metrics_lookup",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = new[] { "UnknownClass123" } });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportObservabilityFeedback_ValidCall_ReturnsConfirmation()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "report_observability_feedback",
            new Dictionary<string, object?>
            {
                ["feedbackType"] = "issue",
                ["title"] = "E2E Test Feedback",
                ["description"] = "Test description from E2E suite.",
                ["relatedTool"] = "find_symbol",
                ["severity"] = "low"
            });

        Assert.Contains("[INFO]: Feedback 'E2E Test Feedback' (issue) erfolgreich protokolliert.", text, StringComparison.Ordinal);
        Assert.Contains("Workaround fortfahren", text, StringComparison.Ordinal);
    }
}

