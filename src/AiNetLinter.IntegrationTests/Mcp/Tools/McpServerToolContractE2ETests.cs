#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
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
    public async Task MetricsLookup_UnknownSymbol_ReturnsContractError()
    {
        var result = await _fixture.Client.CallToolAsync(
            "metrics_lookup",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = new[] { "UnknownClass123" } });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbolHandle_PassesUnchangedToRefactoringDrift()
    {
        var discovery = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greet" },
                ["kind"] = "method",
                ["maxResults"] = 1,
            });
        var handle = ExtractHandoff(discovery);

        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates",
            new Dictionary<string, object?>
            {
                ["mode"] = "refactoring-drift",
                ["helperSymbol"] = handle,
                ["minTokens"] = 1,
                ["maxResults"] = 1,
            });

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("TARGET_MISMATCH", text, StringComparison.Ordinal);
        Assert.DoesNotContain("STALE_SNAPSHOT", text, StringComparison.Ordinal);
    }

    private static string ExtractHandoff(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var match = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException("find_symbol muss ein opaques Handoff ausgeben.");
    }
}

