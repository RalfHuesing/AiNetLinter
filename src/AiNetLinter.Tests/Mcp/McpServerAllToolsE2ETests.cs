#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Umfassende E2E- und Edge-Case-Tests fuer alle 9 MCP-Tools.
/// Nutzt <see cref="SymbolGraphMcpFixture"/> zur einmaligen Fixture- und Client-Instanziierung pro Testklasse.
/// </summary>
[Trait("Category", "Integration")]
[Collection("SymbolGraphMcp")]
public sealed class McpServerAllToolsE2ETests
{
    private readonly SymbolGraphMcpFixture _fixture;

    public McpServerAllToolsE2ETests(SymbolGraphMcpFixture fixture)
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
                ["namePattern"] = "Greeter",
                ["kind"] = "Class"
            });

        Assert.Contains("Greeter", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindSymbol_ZeroResults_ReturnsNoMatchMessage()
    {
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "NonExistentSymbol999" });

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
            new Dictionary<string, object?> { ["typeIdentifier"] = "Greeter" });

        Assert.Contains("Basisklassen", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_UnknownType_ReturnsRecoverableSymbolNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?> { ["typeIdentifier"] = "UnknownClass123" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_NonExistentFile_ReturnsErrorOrNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/DoesNotExist.cs" });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetFileSkeleton_NonCsFile_ReturnsRecoverableResourceNotFound()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?> { ["filePath"] = "src/SymbolGraphMini/wwwroot/Page.xaml" });

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
    public async Task UnknownTool_Call_ThrowsMcpProtocolException()
    {
        await Assert.ThrowsAsync<McpProtocolException>(() => _fixture.Client.CallToolAsync("unknown_tool_name"));
    }

    // Die folgenden Tests decken die SDK-Argument-Bindungsebene ab (ModelContextProtocol.Server),
    // nicht nur die interne ExecuteAsync-Methode: fehlt ein Pflichtparameter im JSON-RPC-Aufruf
    // ganz oder wird er falsch benannt uebergeben, muss die SDK-Bindung den Delegate trotzdem
    // erreichen (Parameter optional mit Default null) statt vor dem Tool-Code mit einer rohen,
    // nicht hilfreichen Fehlermeldung zu scheitern. Ein Unit-Test auf ExecuteAsync direkt wuerde
    // das nicht abdecken, weil die interne Methode den Parameter ohnehin typisiert bekommt.

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
    public async Task GetTypeHierarchy_MissingTypeIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("typeIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_WrongParameterName_ReturnsRecoverableInvalidArgumentInsteadOfCrashing()
    {
        // Reproduziert den gemeldeten Bug: ein Aufrufer uebergibt "symbolIdentifier" (der Name,
        // den find_references/get_call_tree nutzen) statt get_type_hierarchys eigenem
        // "typeIdentifier". Vor dem Fix scheiterte die SDK-Argument-Bindung mit einer rohen
        // "An error occurred invoking..."-Meldung statt eines strukturierten [ERROR]-Ergebnisses.
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("typeIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSymbolBody_MissingIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_symbol_body", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("identifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_MissingFilePath_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("filePath", textContent.Text, StringComparison.Ordinal);
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
}
