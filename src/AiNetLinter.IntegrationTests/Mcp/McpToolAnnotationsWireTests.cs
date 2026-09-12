#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpToolAnnotationsWireTests
{
    [Fact]
    public async Task InitializeAndModernToolsList_ExposeEquivalentAnnotations()
    {
        using var initializeFixture = new SymbolGraphMiniFixtureWorkspace();
        using var modernFixture = new SymbolGraphMiniFixtureWorkspace();

        var initialize = await ReadToolAnnotationsAsync(initializeFixture.SolutionPath, modern: false);
        var modern = await ReadToolAnnotationsAsync(modernFixture.SolutionPath, modern: true);

        Assert.Equal(
            initialize.Keys.OrderBy(name => name, StringComparer.Ordinal),
            modern.Keys.OrderBy(name => name, StringComparer.Ordinal));

        foreach (var name in initialize.Keys)
        {
            Assert.Equal(initialize[name].GetRawText(), modern[name].GetRawText());
        }

        AssertAnnotation(initialize, "find_symbol", readOnly: true, destructive: false, idempotent: true, openWorld: false);
        AssertAnnotation(initialize, "reload_config", readOnly: false, destructive: false, idempotent: true, openWorld: false);
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> ReadToolAnnotationsAsync(
        string targetPath,
        bool modern)
    {
        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            targetPath,
            McpRawWireTestHarness.BuildDiscoveryFrames(modern));
        var response = McpRawWireTestHarness.FindResponse(lines, 2);
        var tools = response.GetProperty("result").GetProperty("tools");

        return tools.EnumerateArray().ToDictionary(
            tool => tool.GetProperty("name").GetString()!,
            tool => tool.GetProperty("annotations").Clone(),
            StringComparer.Ordinal);
    }

    private static void AssertAnnotation(
        IReadOnlyDictionary<string, JsonElement> annotationsByTool,
        string toolName,
        bool readOnly,
        bool destructive,
        bool idempotent,
        bool openWorld)
    {
        var annotations = annotationsByTool[toolName];
        Assert.Equal(readOnly, annotations.GetProperty("readOnlyHint").GetBoolean());
        Assert.Equal(destructive, annotations.GetProperty("destructiveHint").GetBoolean());
        Assert.Equal(idempotent, annotations.GetProperty("idempotentHint").GetBoolean());
        Assert.Equal(openWorld, annotations.GetProperty("openWorldHint").GetBoolean());
    }
}
