#nullable enable

using System.Collections.Generic;
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
}
