#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer fehlende, widerspruechliche und ungueltige Toolargumente.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerArgumentValidationE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerArgumentValidationE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
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

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownTool_Call_ThrowsMcpProtocolException()
    {
        await Assert.ThrowsAsync<McpProtocolException>(() => _fixture.Client.CallToolAsync("unknown_tool_name"));
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
    public async Task MetricsTree_MissingMode_UsesCodeSizeDefault()
    {
        var result = await _fixture.Client.CallToolAsync(
            "metrics_tree", new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal("code_size", result.StructuredContent!.Value.GetProperty("mode").GetString());
    }

    [Theory]
    [InlineData("get_feature_context", "targetType")]
    [InlineData("get_feature_context", "projectRoot")]
    [InlineData("get_feature_context", "configPath")]
    [InlineData("get_feature_context", "assemblyPath")]
    [InlineData("get_feature_context", "ainetlinter.project.json")]
    [InlineData("get_violations", "targetType")]
    [InlineData("get_violations", "projectRoot")]
    [InlineData("get_violations", "configPath")]
    [InlineData("get_violations", "assemblyPath")]
    [InlineData("get_violations", "ainetlinter.project.json")]
    [InlineData("metrics_tree", "targetType")]
    [InlineData("metrics_tree", "projectRoot")]
    [InlineData("metrics_tree", "configPath")]
    [InlineData("metrics_tree", "assemblyPath")]
    [InlineData("metrics_tree", "ainetlinter.project.json")]
    [InlineData("find_symbol", "targetType")]
    [InlineData("find_symbol", "projectRoot")]
    [InlineData("find_symbol", "configPath")]
    [InlineData("find_symbol", "assemblyPath")]
    [InlineData("find_symbol", "ainetlinter.project.json")]
    public async Task TargetPathTools_RejectEveryLegacyArgument(string toolName, string legacyKey)
    {
        var result = await _fixture.Client.CallToolAsync(
            toolName,
            new Dictionary<string, object?> { [legacyKey] = "legacy" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(legacyKey, textContent.Text, StringComparison.Ordinal);
        Assert.Contains("targetPath", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindDuplicates_RefactoringDriftModeWithoutHelperSymbol_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates", new Dictionary<string, object?> { ["mode"] = "refactoring-drift" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("helperSymbol", textContent.Text, StringComparison.Ordinal);
    }
}
