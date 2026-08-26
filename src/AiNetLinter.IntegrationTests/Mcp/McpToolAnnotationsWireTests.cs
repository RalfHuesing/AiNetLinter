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
    public async Task LegacyAndModernToolsList_ExposeEquivalentAnnotations()
    {
        using var legacyFixture = new SymbolGraphMiniFixtureWorkspace();
        using var modernFixture = new SymbolGraphMiniFixtureWorkspace();

        var legacy = await ReadToolAnnotationsAsync(legacyFixture.RootPath, modern: false);
        var modern = await ReadToolAnnotationsAsync(modernFixture.RootPath, modern: true);

        Assert.Equal(
            legacy.Keys.OrderBy(name => name, StringComparer.Ordinal),
            modern.Keys.OrderBy(name => name, StringComparer.Ordinal));

        foreach (var name in legacy.Keys)
        {
            Assert.Equal(legacy[name].GetRawText(), modern[name].GetRawText());
        }

        AssertAnnotation(legacy, "find_symbol", readOnly: true, destructive: false, idempotent: true, openWorld: false);
        AssertAnnotation(legacy, "reload_config", readOnly: false, destructive: false, idempotent: true, openWorld: false);
        AssertAnnotation(legacy, "report_observability_feedback", readOnly: false, destructive: false, idempotent: false, openWorld: false);
    }

    private static async Task<IReadOnlyDictionary<string, JsonElement>> ReadToolAnnotationsAsync(
        string targetDirectory,
        bool modern)
    {
        var lines = await McpRawWireTestHarness.RunAndCollectStdoutAsync(
            targetDirectory,
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
