#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.TestContext;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Registration;

/// <summary>
/// Regressionstests fuer <see cref="McpArgumentValidationFilter.MaximumLimitArgumentsByTool"/>.
/// Verifiziert, dass die Filter-Caps exakt den fachlichen Single-Source-of-Truth-Konstanten
/// der jeweiligen Tools entsprechen.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpArgumentValidationFilterTests
{
    [Fact]
    public void MaximumLimitArguments_MatchToolSpecificSingleSourceOfTruthConstants()
    {
        var limits = McpArgumentValidationFilter.MaximumLimitArgumentsByTool;

        // get_file_tree
        Assert.Equal(GetFileTreeTool.MaxResultsCap, limits["get_file_tree"]["maxResults"]);
        Assert.Equal(GetFileTreeTool.MaxDepthCap, limits["get_file_tree"]["maxDepth"]);
        Assert.Equal(GetFileTreeTool.MaxDepthCap, limits["get_file_tree"]["treeDepth"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_file_tree"]["maxResponseBytes"]);

        // get_class_structure
        Assert.Equal(GetClassStructureTool.MaxMembersCap, limits["get_class_structure"]["maxMembers"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_class_structure"]["maxResponseBytes"]);

        // get_namespace_tree
        Assert.Equal(GetNamespaceTreeTool.MaxResultsCap, limits["get_namespace_tree"]["maxResults"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_namespace_tree"]["maxResponseBytes"]);

        // get_hotspots
        Assert.Equal(GetHotspotsScanner.MaxResultsCap, limits["get_hotspots"]["maxResults"]);

        // search_pattern
        Assert.Equal(SearchPatternTool.MaxResultsCap, limits["search_pattern"]["maxResults"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["search_pattern"]["maxResponseBytes"]);

        // metrics_tree
        Assert.Equal(MetricsTreeTool.MaxDepthCap, limits["metrics_tree"]["depth"]);

        // get_feature_context
        Assert.Equal(FeatureContextScanner.MaxCallersLimit, limits["get_feature_context"]["maxCallers"]);
        Assert.Equal(FeatureContextScanner.MaxTestFilesLimit, limits["get_feature_context"]["maxTests"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_feature_context"]["maxResponseBytes"]);

        // get_test_context
        Assert.Equal(GetTestContextTool.MaxResultsCap, limits["get_test_context"]["maxResults"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_test_context"]["maxResponseBytes"]);

        // get_violations
        Assert.Equal(GetViolationsScanner.MaxContextLines, limits["get_violations"]["contextLines"]);

        // search_assembly
        Assert.Equal(AssemblySearchTool.MaxResultsCap, limits["search_assembly"]["maxResults"]);
        Assert.Equal(GetFileTreeTool.MaxResultsCap, limits["search_assembly"]["maxFiles"]);
        Assert.Equal(AssemblySearchTool.MaxContextLines, limits["search_assembly"]["contextLines"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["search_assembly"]["maxResponseBytes"]);

        // inspect_assembly
        Assert.Equal(AssemblyAnalysisService.MaxResults, limits["inspect_assembly"]["maxResults"]);
        Assert.Equal(AssemblyAnalysisService.MaxMembers, limits["inspect_assembly"]["maxMembers"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["inspect_assembly"]["maxResponseBytes"]);

        // find_assembly_extensions
        Assert.Equal(AssemblyAnalysisService.MaxResults, limits["find_assembly_extensions"]["maxResults"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["find_assembly_extensions"]["maxResponseBytes"]);

        // get_assembly_context
        Assert.Equal(AssemblyAnalysisService.MaxResults, limits["get_assembly_context"]["maxResults"]);
        Assert.Equal(McpResponseBudgetLimits.MaxBytes, limits["get_assembly_context"]["maxResponseBytes"]);
        Assert.Equal(AssemblyAnalysisContextTool.MaxBodyLinesCap, limits["get_assembly_context"]["maxBodyLines"]);
        Assert.Equal(AssemblyAnalysisContextTool.MaxCallersCap, limits["get_assembly_context"]["maxCallers"]);
        Assert.Equal(AssemblyAnalysisContextTool.MaxDepthCap, limits["get_assembly_context"]["depth"]);
        Assert.Equal(AssemblyAnalysisContextTool.MaxTopNCap, limits["get_assembly_context"]["topN"]);
    }

    [Fact]
    public void AllConfiguredTools_WithMaxResponseBytes_UseMcpResponseBudgetLimits()
    {
        foreach (var (toolName, toolLimits) in McpArgumentValidationFilter.MaximumLimitArgumentsByTool)
        {
            if (toolLimits.TryGetValue("maxResponseBytes", out var maxResponseBytes))
            {
                Assert.True(
                    maxResponseBytes == McpResponseBudgetLimits.MaxBytes,
                    $"Tool '{toolName}' verwendet nicht McpResponseBudgetLimits.MaxBytes fuer maxResponseBytes.");
            }
        }
    }

    [Theory]
    [InlineData("find_references", "symbolIdentifier")]
    [InlineData("get_call_tree", "symbolIdentifier")]
    [InlineData("get_type_hierarchy", "symbolIdentifier")]
    [InlineData("get_symbol_body", "symbolIdentifiers")]
    [InlineData("get_file_skeleton", "filePaths")]
    [InlineData("search_pattern", "pattern")]
    public void ValidateArguments_MissingRequiredField_ReturnsPreciseErrorWithNavigation(
        string toolName,
        string requiredField)
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            toolName,
            Schema(requiredField),
            new Dictionary<string, JsonElement>());

        AssertFilterError(error, $"$.{requiredField}");
        var projected = McpArgumentValidationFilter.ProjectFilterError(error!, null, "C:\\virtual\\fixture.slnx");
        Assert.True(projected.StructuredContent!.Value.TryGetProperty("navigation", out _));
    }

    [Theory]
    [InlineData("get_type_hierarchy", "wrongParam")]
    [InlineData("get_feature_context", "unexpectedOption")]
    [InlineData("get_violations", "unrecognizedField")]
    [InlineData("find_symbol", "unknownInput")]
    [InlineData("get_file_skeleton", "filePath")]
    public void ValidateArguments_UnknownArgument_ReturnsPreciseFieldPath(string toolName, string unknownField)
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            toolName,
            Schema(),
            Arguments($"{{\"{unknownField}\":\"value\"}}"));

        AssertFilterError(error, $"$.{unknownField}");
    }

    [Theory]
    [InlineData("find_symbol", "namePatterns", "\"not-an-array\"")]
    [InlineData("get_file_tree", "includeExtensions", "\"not-an-array\"")]
    [InlineData("get_server_health", "includeDiagnostics", "\"not-a-boolean\"")]
    public void ValidateArguments_WrongJsonType_ReturnsPreciseFieldPath(
        string toolName,
        string fieldName,
        string value)
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            toolName,
            Schema(fieldName),
            Arguments($"{{\"{fieldName}\":{value}}}"));

        AssertFilterError(error, $"$.{fieldName}");
    }

    [Fact]
    public void ValidateArguments_WrongArrayElementType_ReturnsIndexedFieldPath()
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            "find_symbol",
            Schema("namePatterns"),
            Arguments("{\"namePatterns\":[\"Greeter\",42]}"));

        AssertFilterError(error, "$.namePatterns[1]");
    }

    [Theory]
    [InlineData("find_symbol", "maxResults", 0, "$.maxResults")]
    [InlineData("get_call_tree", "depth", 0, "$.depth")]
    [InlineData("get_call_tree", "topN", 0, "$.topN")]
    [InlineData("find_duplicates", "maxResults", -5, "$.maxResults")]
    [InlineData("get_namespace_tree", "maxResponseBytes", -1, "$.maxResponseBytes")]
    [InlineData("get_violations", "contextLines", -1, "$.contextLines")]
    [InlineData("get_test_context", "maxResults", 101, "$.maxResults")]
    [InlineData("get_namespace_tree", "maxResults", 201, "$.maxResults")]
    public void ValidateArguments_LimitOutsideContract_ReturnsPreciseFieldPath(
        string toolName,
        string fieldName,
        int value,
        string fieldPath)
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            toolName,
            Schema(fieldName),
            Arguments($"{{\"{fieldName}\":{value}}}"));

        AssertFilterError(error, fieldPath);
    }

    [Fact]
    public void ValidateArguments_ResponseBudgetBelowPublicMinimum_ReturnsPreciseFieldPath()
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            "find_symbol",
            Schema("maxResponseBytes"),
            Arguments("{\"maxResponseBytes\":511}"));

        AssertFilterError(error, "$.maxResponseBytes");
    }

    [Fact]
    public void ValidateArguments_SafeguardNonPositiveMaxViolations_PreservesToolCompatibility()
    {
        var error = McpArgumentValidationFilter.ValidateArguments(
            "safeguard",
            Schema("maxViolations"),
            Arguments("{\"maxViolations\":0}"));

        Assert.Null(error);
    }

    private static JsonElement Schema(string? propertyName = null)
    {
        var property = propertyName switch
        {
            null => "{}",
            "namePatterns" => "{\"namePatterns\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}}",
            "includeExtensions" => "{\"includeExtensions\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}}",
            "includeDiagnostics" => "{\"includeDiagnostics\":{\"type\":\"boolean\"}}",
            _ => $"{{\"{propertyName}\":{{\"type\":\"integer\"}}}}",
        };
        var required = propertyName is null ? "[]" : $"[\"{propertyName}\"]";
        return JsonDocument.Parse($"{{\"properties\":{property},\"required\":{required}}}").RootElement.Clone();
    }

    private static IDictionary<string, JsonElement> Arguments(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static void AssertFilterError(ModelContextProtocol.Protocol.CallToolResult? result, string fieldPath)
    {
        Assert.NotNull(result);
        Assert.NotEqual(true, result!.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal(fieldPath, result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }
}
