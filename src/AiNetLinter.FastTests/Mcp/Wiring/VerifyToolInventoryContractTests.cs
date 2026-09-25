#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Projects;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Wiring;

[Trait("Category", "Unit")]
public sealed class VerifyToolInventoryContractTests
{
    [Fact]
    public async Task ToolCollection_PublishesVerifyAsTheOnlyQualityGateSurface()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions)),
                assemblyRegistry: composition.Sessions)
            .ToDictionary(tool => tool.ProtocolTool.Name, StringComparer.Ordinal);

        var verify = Assert.Single(tools.Where(pair => pair.Key == "verify")).Value.ProtocolTool;
        Assert.Contains("scope", verify.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("changes", verify.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("solution", verify.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("minScore", verify.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("maxResults", verify.InputSchema.ToString(), StringComparison.Ordinal);

        var advisories = Assert.Single(tools.Where(pair => pair.Key == "get_verify_advisories")).Value.ProtocolTool;
        Assert.Contains("category", advisories.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("dead_code", advisories.Description, StringComparison.Ordinal);
        Assert.Contains("64 KiB", advisories.Description, StringComparison.Ordinal);

        var retiredNames = new[] { "safeguard", "get_violations", "find_magic_values", "find_dead_code" };
        foreach (var retiredName in retiredNames)
        {
            Assert.DoesNotContain(retiredName, tools.Keys, StringComparer.Ordinal);
        }

        var untouchedNames = new[] { "pattern_detect", "get_hotspots", "metrics_tree", "metrics_lookup" };
        foreach (var untouchedName in untouchedNames)
        {
            Assert.Contains(untouchedName, tools.Keys, StringComparer.Ordinal);
        }
    }
}
