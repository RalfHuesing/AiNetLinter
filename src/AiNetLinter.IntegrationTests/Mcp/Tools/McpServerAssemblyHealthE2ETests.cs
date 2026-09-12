#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Repräsentative E2E-Verträge für Assembly-Metadaten, Health und Registrierung.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class McpServerAssemblyHealthE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerAssemblyHealthE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InspectAssembly_StandaloneCallReturnsStructuredMetadata()
    {
        var result = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["typeName"] = nameof(McpCodeGraphServer),
                ["publicOnly"] = false,
                ["exactTypeName"] = true,
                ["memberNames"] = new[] { "Dispose" },
                ["maxMembers"] = 10,
                ["maxResponseBytes"] = 12_000,
            });

        Assert.False(result.IsError == true, result.ToString());
        var payload = result.StructuredContent!.Value;
        var types = payload.GetProperty("types");
        Assert.Single(types.EnumerateArray());
        Assert.Equal(nameof(McpCodeGraphServer), types[0].GetProperty("name").GetString());
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Assembly:", text, StringComparison.Ordinal);
        Assert.Contains("API-Typen:", text, StringComparison.Ordinal);
        AssertAssemblyNavigation(payload);
    }

    [Fact]
    public async Task InspectAssembly_DoesNotExposeMaterializationPaths()
    {
        var result = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["typeName"] = nameof(McpCodeGraphServer),
                ["exactTypeName"] = true,
                ["publicOnly"] = false,
                ["maxMembers"] = 10,
            });

        Assert.False(result.IsError == true, result.ToString());
        var structured = result.StructuredContent!.Value.GetRawText();
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        foreach (var forbidden in new[]
                 {
                     "cursor", "isTruncated", "generation", "generatedPath", "decompiledProjectDirectory",
                     "decompiledProjectPath", "decompiledSourceRoot",
                 })
        {
            Assert.DoesNotContain(forbidden, structured, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task InspectAssembly_RegistrationAdvertisesGenericFiltersAndParameterMetadata()
    {
        var tools = await _fixture.Client.ListToolsAsync();
        var inspect = Assert.Single(tools.Where(candidate => candidate.ProtocolTool.Name == "inspect_assembly"));
        var schema = inspect.ProtocolTool.InputSchema.ToString();
        Assert.Contains("exactTypeName", schema, StringComparison.Ordinal);
        Assert.Contains("memberNames", schema, StringComparison.Ordinal);
        Assert.Contains("maxMembers", schema, StringComparison.Ordinal);
        Assert.Contains("strukturierte Parameterdaten", inspect.ProtocolTool.Description, StringComparison.Ordinal);

        var health = Assert.Single(tools.Where(candidate => candidate.ProtocolTool.Name == "get_server_health"));
        Assert.Contains("includeDiagnostics", health.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("maxDiagnostics", health.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("includeSessions", health.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);

        foreach (var name in new[] { "inspect_assembly", "find_assembly_extensions", "search_assembly", "get_assembly_context" })
        {
            var candidate = Assert.Single(tools.Where(tool => tool.ProtocolTool.Name == name));
            Assert.DoesNotContain("\"cursor\"", candidate.ProtocolTool.InputSchema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("includeSessions", candidate.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("isTruncated", candidate.ProtocolTool.Description, StringComparison.Ordinal);
        }
    }

    private static void AssertAssemblyNavigation(System.Text.Json.JsonElement payload)
    {
        var navigation = payload.GetProperty("navigation");
        Assert.Equal("assembly", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
    }
}
