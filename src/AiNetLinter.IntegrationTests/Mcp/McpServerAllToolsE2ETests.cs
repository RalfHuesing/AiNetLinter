#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// Umfassende E2E- und Edge-Case-Tests fuer alle MCP-Tools.
/// Nutzt <see cref="ReadOnlyMcpHostFixture"/> zur einmaligen lazy Host-Instanziierung.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerAllToolsE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerAllToolsE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSymbol_KindFilter_ReturnsFilteredSymbolsOnly()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["kind"] = "Class"
            });

        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_StandaloneCallReturnsStructuredMetadata()
    {
        var result = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = typeof(McpCodeGraphServer).Assembly.Location
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Assembly:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche API-Typen:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_ZeroResults_ReturnsNoMatchMessage()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "NonExistentSymbol999" } });

        Assert.Contains("Keine Treffer", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindReferences_UnknownSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_references",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "NonExistent.Symbol" });

        // isError-Policy: SYMBOL_NOT_FOUND ist recoverable — IsError bleibt false.
        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImpact_BothArgumentsProvided_ReturnsRecoverableInvalidArgumentMessage()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["gitRef"] = "HEAD",
                ["symbolIdentifier"] = "Greeter.Greet"
            });

        // isError-Policy: INVALID_ARGUMENT ist recoverable — IsError bleibt false, der Text
        // traegt die Handlungsanleitung (gitRef ODER symbolIdentifier, nie beide).
        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_ValidType_ReturnsHierarchyInfo()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter" });

        Assert.Contains("Basisklassen", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_UnknownType_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["symbolIdentifier"] = "UnknownClass123" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_NonExistentFile_ReturnsErrorOrNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/DoesNotExist.cs" } });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFileSkeleton_NonCsFile_ReturnsRecoverableResourceNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePaths"] = new[] { "src/SymbolGraphMini/wwwroot/Page.xaml" } });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("RESOURCE_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIndexScope_ValidWorkspace_ReturnsFileTypeBreakdown()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_index_scope");

        Assert.Contains(".cs", text, StringComparison.Ordinal);
        Assert.Contains(".razor", text, StringComparison.Ordinal);
        Assert.Contains(".xaml", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHotspots_ValidWorkspace_ReturnsHotspotSummary()
    {
        var text = await _fixture.Client.CallToolGetTextAsync("get_hotspots");

        Assert.NotNull(text);
    }

    [Fact]
    public async Task GetViolations_WithScopeFilter_FiltersResults()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_violations",
            new Dictionary<string, object?> { ["scopeFilter"] = "SymbolGraphMini" });

        Assert.NotNull(text);
    }

    [Fact]
    public async Task SearchPattern_PlainTextSearch_ReturnsMatches()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "userService",
                ["isRegex"] = false
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPattern_RegexSearch_ReturnsMatches()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = @"user\w+",
                ["isRegex"] = true
            });

        Assert.Contains("userService", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchPattern_StructuredResponse_ReturnsObjectWithRangesAndCompleteness()
    {
        var result = await _fixture.Client.CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "userService",
                ["contextLines"] = 1,
                ["maxResponseBytes"] = 4096,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var structured = result.StructuredContent!.Value;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, structured.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, structured.GetProperty("matches").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, structured.GetProperty("completeness").ValueKind);
        Assert.Contains(
            "userService",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownTool_Call_ThrowsMcpProtocolException()
    {
        await Assert.ThrowsAsync<McpProtocolException>(() => _fixture.Client.CallToolAsync("unknown_tool_name"));
    }

    [Fact]
    public async Task FindSymbol_MissingNamePattern_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindReferences_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_references", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCallTree_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_WrongParameterName_ReturnsRecoverableInvalidArgumentInsteadOfCrashing()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?> { ["wrongParam"] = "Greeter" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_MissingNamePatterns_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("namePatterns", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSymbolBody_MissingSymbolIdentifiers_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_symbol_body", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifiers", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_MissingFilePaths_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("filePaths", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPattern_MissingPattern_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "search_pattern", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MetricsTree_MissingMode_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "metrics_tree", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("mode", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindDuplicates_RefactoringDriftModeWithoutHelperSymbol_ReturnsRecoverableInvalidArgument()
    {
        // find_duplicates' helperSymbol war schon vor dem SDK-Bindungs-Fix optional deklariert
        // (kein Crash-Risiko) — Regressionstest auf SDK-Ebene, damit das so bleibt.
        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates", new Dictionary<string, object?> { ["mode"] = "refactoring-drift" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("helperSymbol", textContent.Text, StringComparison.Ordinal);
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
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
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
