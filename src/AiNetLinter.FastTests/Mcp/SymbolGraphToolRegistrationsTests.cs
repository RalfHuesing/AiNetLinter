#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Regressionstests fuer die in <see cref="SymbolGraphToolRegistrations"/> gepflegten
/// Tool-Beschreibungen. Sichert ab, dass der 200-Knoten-Hard-Cap aus
/// <c>CallGraphTraversal.MaxRecursionNodes</c> fuer die beiden Tools, die ihn tatsaechlich
/// nutzen (<c>find_references</c> und <c>get_impact</c>), im Tool-Schema dokumentiert ist —
/// sonst sieht ein Agent das Limit erst in der Trunkierungs-Meta-Zeile, nachdem der Cap
/// bereits erreicht wurde.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SymbolGraphToolRegistrationsTests
{
    [Fact]
    public void FindSymbolSchema_AdvertisesScopeAndGeneratedControls()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == "find_symbol").ProtocolTool;
        var properties = tool.InputSchema.GetProperty("properties");

        Assert.True(properties.TryGetProperty("scopeType", out var scopeType));
        Assert.Contains(
            "string",
            scopeType.GetProperty("type").ValueKind == System.Text.Json.JsonValueKind.Array
                ? scopeType.GetProperty("type").EnumerateArray().Select(item => item.GetString())
                : [scopeType.GetProperty("type").GetString()],
            StringComparer.Ordinal);
        Assert.True(properties.TryGetProperty("includeGenerated", out var includeGenerated));
        Assert.Equal("boolean", includeGenerated.GetProperty("type").GetString());
        Assert.Contains("scopeType", tool.Description, StringComparison.Ordinal);
        Assert.Contains("includeGenerated", tool.Description, StringComparison.Ordinal);
        Assert.Contains("16", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferencesHierarchyAndImplementationsSchemas_AdvertiseScopeAndGeneratedControls()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        foreach (var toolName in new[] { "find_references", "get_type_hierarchy", "find_implementations" })
        {
            var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == toolName).ProtocolTool;
            var properties = tool.InputSchema.GetProperty("properties");
            Assert.True(properties.TryGetProperty("scopeType", out var scopeType));
            Assert.Contains("string", scopeType.GetProperty("type").ToString(), StringComparison.Ordinal);
            Assert.True(properties.TryGetProperty("includeGenerated", out var includeGenerated));
            Assert.Equal("boolean", includeGenerated.GetProperty("type").GetString());
            Assert.Contains("scopeType", tool.Description, StringComparison.Ordinal);
            Assert.Contains("includeGenerated", tool.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ToolDescriptions_FindReferencesAndGetImpact_MentionNodeHardCap()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        var descriptions = options.ToolCollection!
            .ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.Description!);

        Assert.Contains("200", descriptions["find_references"], StringComparison.Ordinal);
        Assert.Contains("200", descriptions["get_impact"], StringComparison.Ordinal);
        foreach (var toolName in new[] { "find_symbol", "find_references", "get_call_tree" })
        {
            var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == toolName);
            Assert.Contains("includeReferences", tool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        }
        foreach (var toolName in new[] { "find_references", "get_call_tree", "get_impact", "get_type_hierarchy", "dependency_graph", "get_class_structure" })
        {
            var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == toolName);
            Assert.Contains("\"symbolIdentifier\"", tool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        }
        foreach (var toolName in new[] { "get_symbol_body", "metrics_lookup" })
        {
            var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == toolName);
            Assert.Contains("\"symbolIdentifiers\"", tool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        }
    }
}
