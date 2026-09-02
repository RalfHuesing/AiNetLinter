#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Wiring;

/// <summary>
/// Vertragstests fuer Toolbestand, Target-Capabilities und MCP-Annotations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WiringToolCollectionContractTests
{
    [Fact]
    public async Task ToolCollection_FreezesInventoryAndProjectRootContract()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions))),
            McpServerResourceCollectionFactory.Build(registry));
        var tools = options.ToolCollection!.ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool);
        Assert.Equal(29, tools.Count);
        foreach (var tool in tools.Values)
        {
            var required = GetRequiredProperties(tool.InputSchema);
            if (tool.Name == "report_observability_feedback")
            {
                Assert.DoesNotContain("targetType", tool.InputSchema.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("targetPath", tool.InputSchema.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"projectRoot\"", tool.InputSchema.ToString(), StringComparison.Ordinal);
            }
            else if (tool.Name == "get_server_health")
            {
                Assert.DoesNotContain("targetType", required);
                Assert.DoesNotContain("targetPath", required);
                Assert.Contains("\"targetType\"", tool.InputSchema.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"targetPath\"", tool.InputSchema.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("projectRoot", tool.InputSchema.ToString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("targetType", required);
                Assert.Contains("targetPath", required);
                Assert.DoesNotContain("projectRoot", tool.InputSchema.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("assemblyPath", tool.InputSchema.ToString(), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task ToolCollection_AdvertisesCompleteProjectAssemblyCapabilityMatrix()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions)),
                assemblyRegistry: composition.Sessions)
            .ToDictionary(tool => tool.ProtocolTool.Name, tool => tool.ProtocolTool);

        var projectAndAssembly = new[]
        {
            "dependency_graph", "find_references", "find_symbol", "get_call_tree",
            "get_class_structure", "get_file_skeleton", "get_impact", "get_namespace_tree", "get_symbol_body",
            "get_type_hierarchy", "metrics_lookup", "metrics_tree",
        };
        var projectOnly = new[]
        {
            "find_dead_code", "find_duplicates", "find_magic_values", "get_feature_context",
            "get_file_tree", "get_hotspots", "get_index_scope", "get_test_context",
            "get_violations", "pattern_detect", "reload_config", "safeguard", "search_pattern",
        };

        foreach (var name in projectAndAssembly)
        {
            var description = tools[name].Description;
            Assert.Contains("targetType='project'", description, StringComparison.Ordinal);
            Assert.Contains("targetType='assembly'", description, StringComparison.Ordinal);
            Assert.Contains("Snapshot/Generation", description, StringComparison.Ordinal);
            Assert.DoesNotContain("ausdrücklich unsupported", description, StringComparison.Ordinal);
        }

        foreach (var name in projectOnly)
        {
            var description = tools[name].Description;
            Assert.Contains("targetType='project'", description, StringComparison.Ordinal);
            Assert.Contains("ausdrücklich unsupported", description, StringComparison.Ordinal);
        }

        foreach (var name in new[] { "inspect_assembly", "find_assembly_extensions" })
        {
            var description = tools[name].Description;
            Assert.Contains("targetType='assembly'", description, StringComparison.Ordinal);
            Assert.DoesNotContain("targetType='project'", description, StringComparison.Ordinal);
            Assert.Contains(".dll", description, StringComparison.Ordinal);
            Assert.Contains(".exe", description, StringComparison.Ordinal);
        }

        Assert.Contains("Projekt- und Assembly-Sessions", tools["get_server_health"].Description, StringComparison.Ordinal);
        Assert.Contains("targetType='assembly'", tools["get_server_health"].Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolCollection_AdvertisesP2ArgumentAliasesAndDefaults()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions)),
                assemblyRegistry: composition.Sessions)
            .ToDictionary(tool => tool.ProtocolTool.Name, tool => tool.ProtocolTool);

        var extensions = tools["find_assembly_extensions"];
        Assert.Contains("includeReferences", extensions.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("Default false", extensions.Description, StringComparison.Ordinal);

        var skeleton = tools["get_file_skeleton"];
        Assert.Contains("filePaths", skeleton.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("filePath", skeleton.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("String-Alias", skeleton.Description, StringComparison.Ordinal);

        var metrics = tools["metrics_tree"];
        Assert.Contains("\"mode\"", metrics.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("code_size", metrics.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("Default", metrics.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolCollection_AdvertisesHotspotBoundsAndPrimarySymbolIdentifierNames()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var targetRoute = AnalysisToolCall.CreateTargetRoute(
            ProjectAnalysisDispatcher.CreateRoute(registry),
            AssemblyAnalysisDispatcher.CreateRoute(null));
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(registry, targetRoute),
            McpServerResourceCollectionFactory.Build(registry));
        var tools = options.ToolCollection!.ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool);

        var hotspots = tools["get_hotspots"];
        Assert.Contains("maxResults", hotspots.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("minLinePercentage", hotspots.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("Default 50, Cap 200", hotspots.Description, StringComparison.Ordinal);
        Assert.Contains("Default 80, Bereich 0-100", hotspots.Description, StringComparison.Ordinal);

        var featureContext = tools["get_feature_context"];
        Assert.Contains("symbolIdentifier", featureContext.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("symbol", featureContext.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier (primaer", featureContext.Description, StringComparison.Ordinal);

        var testContext = tools["get_test_context"];
        Assert.Contains("symbolIdentifier", testContext.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("symbol", testContext.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("symbolIdentifier (primaer", testContext.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolCollection_ClassifiesEveryRegisteredToolWithExplicitAnnotations()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null)))
            .ToDictionary(tool => tool.ProtocolTool.Name, StringComparer.Ordinal);
        Assert.Equal(
            ExpectedToolAnnotations.Keys.OrderBy(name => name, StringComparer.Ordinal),
            tools.Keys.OrderBy(name => name, StringComparer.Ordinal));
        foreach (var (name, expected) in ExpectedToolAnnotations)
        {
            var annotations = tools[name].ProtocolTool.Annotations;
            Assert.NotNull(annotations);
            Assert.Equal(expected.ReadOnly, annotations!.ReadOnlyHint);
            Assert.Equal(expected.Destructive, annotations.DestructiveHint);
            Assert.Equal(expected.Idempotent, annotations.IdempotentHint);
            Assert.Equal(expected.OpenWorld, annotations.OpenWorldHint);
        }
    }

    private static readonly IReadOnlyDictionary<string, ToolAnnotationExpectation> ExpectedToolAnnotations =
        new Dictionary<string, ToolAnnotationExpectation>(StringComparer.Ordinal)
        {
            ["dependency_graph"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_assembly_extensions"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_dead_code"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_duplicates"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_magic_values"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_references"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_symbol"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_call_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_class_structure"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_feature_context"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_file_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_file_skeleton"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_hotspots"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_index_scope"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_namespace_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_server_health"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_symbol_body"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_test_context"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_type_hierarchy"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_impact"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_violations"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["metrics_lookup"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["metrics_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["pattern_detect"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["reload_config"] = new(false, false, true, false),
            ["report_observability_feedback"] = new(false, false, false, false),
            ["safeguard"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["search_pattern"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["inspect_assembly"] = ToolAnnotationExpectation.ReadOnlyProfile,
        };

    private readonly record struct ToolAnnotationExpectation(
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld)
    {
        internal static ToolAnnotationExpectation ReadOnlyProfile => new(true, false, true, false);
    }

    private static string[] GetRequiredProperties(System.Text.Json.JsonElement inputSchema)
    {
        using var document = System.Text.Json.JsonDocument.Parse(inputSchema.ToString());
        if (!document.RootElement.TryGetProperty("required", out var required)
            || required.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return [];
        }

        return required.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }
}
