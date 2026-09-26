#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Registration;

[Trait("Category", "Unit")]
public sealed class AssemblyLongRunningSchemaTests
{
    [Fact]
    public void AssemblyTools_ExposeOperationTokenForLongDecompilation()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        foreach (var name in new[] { "search_assembly", "inspect_assembly", "find_assembly_extensions", "get_assembly_context" })
        {
            var tool = options.ToolCollection!.Single(item => item.ProtocolTool.Name == name).ProtocolTool;
            Assert.True(tool.InputSchema.GetProperty("properties").TryGetProperty("operationToken", out _), name);
            Assert.Contains("operationToken", tool.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OperationIdentity_IgnoresOnlyPollTokenAndArgumentOrder()
    {
        var first = new Dictionary<string, JsonElement>
        {
            ["targetPath"] = JsonSerializer.SerializeToElement("C:/a.dll"),
            ["maxResults"] = JsonSerializer.SerializeToElement(20),
        };
        var resumed = new Dictionary<string, JsonElement>
        {
            ["operationToken"] = JsonSerializer.SerializeToElement("opaque"),
            ["maxResults"] = JsonSerializer.SerializeToElement(20),
            ["targetPath"] = JsonSerializer.SerializeToElement("C:/a.dll"),
        };

        Assert.Equal(LongRunningAssemblyToolCall.CreateArgumentsKey(first),
            LongRunningAssemblyToolCall.CreateArgumentsKey(resumed));
        resumed["maxResults"] = JsonSerializer.SerializeToElement(21);
        Assert.NotEqual(LongRunningAssemblyToolCall.CreateArgumentsKey(first),
            LongRunningAssemblyToolCall.CreateArgumentsKey(resumed));
    }
}
