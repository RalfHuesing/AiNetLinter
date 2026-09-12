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
public sealed partial class McpServerArgumentValidationE2ETests
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

        Assert.True(result.IsError, result.ToString());
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

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TargetPathTool_MissingTargetPathReturnsFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolWithoutTargetAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "Greeter" } });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("targetPath", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.targetPath", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task GetCallTree_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTypeHierarchy_MissingSymbolIdentifier_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier", textContent.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrongParam")]
    [InlineData("anotherUnknownField")]
    public async Task GetTypeHierarchy_UnknownParameter_ReturnsRecoverableInvalidArgument(string unknownName)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_type_hierarchy", new Dictionary<string, object?> { [unknownName] = "Greeter" });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains($"Unbekanntes Argument: {unknownName}", textContent.Text, StringComparison.Ordinal);
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
        Assert.Equal("$.namePatterns", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task FindSymbol_ZeroMaxResults_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["maxResults"] = 0,
            });

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("maxResults muss mindestens 1 sein.", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.maxResults", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task FindSymbol_InvalidKindAndEmptyBatchElementReturnPrecisePaths_AndValidRetrySucceeds()
    {
        var invalidKind = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["kind"] = "trait",
            });

        Assert.Equal("INVALID_ARGUMENT", invalidKind.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.kind", invalidKind.StructuredContent.Value.GetProperty("fieldPath").GetString());

        var emptyElement = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePatterns"] = new[] { "" } });

        Assert.Equal("INVALID_ARGUMENT", emptyElement.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.namePatterns[0]", emptyElement.StructuredContent.Value.GetProperty("fieldPath").GetString());

        var retry = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new[] { "Greeter" },
                ["kind"] = "class",
            });

        Assert.False(retry.IsError == true, retry.ToString());
        Assert.True(retry.StructuredContent!.Value.TryGetProperty("results", out _), retry.StructuredContent.Value.GetRawText());
    }

    [Fact]
    public async Task GetSymbolBody_MissingSymbolIdentifiers_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_symbol_body", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("symbolIdentifiers", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileSkeleton_MissingFilePaths_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("filePaths", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPattern_MissingPattern_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "search_pattern", new Dictionary<string, object?>());

        Assert.True(result.IsError, result.ToString());
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
    [InlineData("get_feature_context", "unexpectedOption")]
    [InlineData("get_violations", "unrecognizedField")]
    [InlineData("metrics_tree", "futureParameter")]
    [InlineData("find_symbol", "unknownInput")]
    [InlineData("find_references", "symbol")]
    [InlineData("get_call_tree", "symbol")]
    [InlineData("get_type_hierarchy", "symbol")]
    [InlineData("dependency_graph", "symbol")]
    [InlineData("search_pattern", "query")]
    [InlineData("find_magic_values", "query")]
    [InlineData("get_file_skeleton", "filePath")]
    public async Task TargetPathTools_RejectUnknownArguments(string toolName, string unknownKey)
    {
        var arguments = new Dictionary<string, object?> { [unknownKey] = "unknown" };
        if (toolName == "get_feature_context") arguments["symbolIdentifier"] = "Greeter.Greet";

        var result = await _fixture.Client.CallToolAsync(
            toolName,
            arguments);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains($"Unbekanntes Argument: {unknownKey}", textContent.Text, StringComparison.Ordinal);
        Assert.Equal(
            $"$.{unknownKey}",
            result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task FindDuplicates_RefactoringDriftModeWithoutHelperSymbol_ReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates", new Dictionary<string, object?> { ["mode"] = "refactoring-drift" });

        Assert.True(result.IsError, result.ToString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("helperSymbol", textContent.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("find_symbol", "namePatterns", "array")]
    [InlineData("get_file_tree", "includeExtensions", "array")]
    [InlineData("get_server_health", "includeDiagnostics", "boolean")]
    public async Task WrongArgumentType_ReturnsFieldAwareRecoverableInvalidArgument(
        string toolName,
        string fieldName,
        string expectedType)
    {
        var result = await _fixture.Client.CallToolAsync(
            toolName,
            new Dictionary<string, object?> { [fieldName] = "not-the-declared-type" });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains(fieldName, textContent.Text, StringComparison.Ordinal);
        Assert.Contains(expectedType, textContent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "INVALID_ARGUMENT",
            result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal(
            $"$.{fieldName}",
            result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task WrongArrayElementType_ReturnsIndexedFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePatterns"] = new object?[] { "Greeter", 42 },
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("namePatterns[1]", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.namePatterns[1]", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
        Assert.Contains("string", result.StructuredContent.Value.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallTree_InvalidFormatReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                ["format"] = "json",
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.True(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.format", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task CallTree_NonIntegerDepthReturnsFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                ["depth"] = 1.5,
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.depth", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
        Assert.Contains("Int32", result.StructuredContent.Value.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamespaceTree_ExceedsAdvertisedCapReturnsFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["project"] = "SymbolGraphMini",
                ["maxResults"] = 201,
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.maxResults", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData("get_namespace_tree")]
    [InlineData("get_file_skeleton")]
    [InlineData("get_class_structure")]
    public async Task DiscoveryTools_NegativeResponseBudgetReturnsFieldAwareInvalidArgument(string toolName)
    {
        var arguments = new Dictionary<string, object?> { ["maxResponseBytes"] = -1 };
        if (toolName == "get_file_skeleton") arguments["filePaths"] = new[] { "src/SymbolGraphMini/Greeter.cs" };
        if (toolName == "get_class_structure") arguments["symbolIdentifier"] = "Greeter";

        var result = await _fixture.Client.CallToolAsync(toolName, arguments);

        Assert.False(result.IsError == true, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxResponseBytes", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task NamespaceTree_TinyPositiveResponseBudgetReturnsErrorBeforeDispatch()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?> { ["maxResponseBytes"] = 511 });

        Assert.True(result.IsError, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxResponseBytes", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Safeguard_NonPositiveMaxViolationsKeepsFeatureSemantics(int maxViolations)
    {
        var result = await _fixture.Client.CallToolAsync(
            "safeguard",
            new Dictionary<string, object?> { ["maxViolations"] = maxViolations });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.DoesNotContain("maxViolations muss mindestens 1 sein", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetViolations_InvalidMinSeverityReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_violations",
            new Dictionary<string, object?>
            {
                ["minSeverity"] = "trace",
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.True(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.minSeverity", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task GetViolations_ContextLinesOutsideRangeReturnsFieldAwareInvalidArgument(int contextLines)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_violations",
            new Dictionary<string, object?> { ["contextLines"] = contextLines });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.contextLines", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task GetTestContext_ExceedingMaxResultsCapReturnsFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_test_context",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter",
                ["maxResults"] = 101,
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.maxResults", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData("topN")]
    [InlineData("depth")]
    public async Task CallTree_NonPositiveLimitReturnsFieldAwareInvalidArgument(string fieldName)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "Greeter.Greet",
                [fieldName] = 0,
            });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal($"$.{fieldName}", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task FindDuplicates_NonPositiveMaxResultsReturnsFieldAwareInvalidArgument(int maxResults)
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_duplicates",
            new Dictionary<string, object?> { ["maxResults"] = maxResults });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.False(result.IsError, textContent.Text);
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Equal("$.maxResults", result.StructuredContent!.Value.GetProperty("fieldPath").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task FindMagicValues_NonPositiveMaxResultsPreservesClampCompatibility(int maxResults)
    {
        var result = await _fixture.Client.CallToolAsync(
            "find_magic_values",
            new Dictionary<string, object?> { ["maxResults"] = maxResults });

        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.NotEqual(true, result.IsError);
        Assert.DoesNotContain("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.NotNull(result.StructuredContent);
    }
}
