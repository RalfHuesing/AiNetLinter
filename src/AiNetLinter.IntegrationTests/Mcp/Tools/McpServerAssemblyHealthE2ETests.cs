#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer Assembly-Analyse und die projekt-/assemblybezogene Health-Sicht.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerAssemblyHealthE2ETests
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
                ["maxMembers"] = 10
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.True(result.StructuredContent!.Value.TryGetProperty("types", out var types), result.StructuredContent.Value.GetRawText());
        Assert.Single(types.EnumerateArray());
        Assert.Equal(nameof(McpCodeGraphServer), types[0].GetProperty("name").GetString());
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Assembly:", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("API-Typen:", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Öffentliche API-Typen:", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Öffentliche Namespaces:", textContent.Text, StringComparison.Ordinal);
        AssertPublicAssemblyWire(result);
        AssertAssemblyNavigation(result, textContent.Text);

        var extensions = await _fixture.Client.CallToolAsync(
            "find_assembly_extensions",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["maxResults"] = 10
            });
        Assert.NotEqual(true, extensions.IsError);
        Assert.Contains(
            "Assembly-Extensions:",
            Assert.IsType<TextContentBlock>(Assert.Single(extensions.Content)).Text,
            StringComparison.Ordinal);
        AssertPublicAssemblyWire(extensions);

        var context = await _fixture.Client.CallToolAsync(
            "get_assembly_context",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["maxResults"] = 3,
            });
        Assert.False(context.IsError == true, string.Join("\n", context.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        AssertPublicAssemblyWire(context);
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

        Assert.False(result.IsError == true, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.False(payload.TryGetProperty("decompiledProjectDirectory", out _), payload.GetRawText());
        Assert.False(payload.TryGetProperty("decompiledProjectPath", out _), payload.GetRawText());
        Assert.False(payload.TryGetProperty("decompiledSourceRoot", out _), payload.GetRawText());

        var tree = await _fixture.Client.CallToolAsync(
            "get_file_tree",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["view"] = "files",
                ["includeExtensions"] = new[] { ".cs" },
                ["fileFilter"] = "**/*.cs",
                ["maxDepth"] = 32,
                ["maxResults"] = 2000,
            });

        Assert.NotEqual(true, tree.IsError);
        Assert.NotNull(tree.StructuredContent);
        var treePayload = tree.StructuredContent!.Value.GetProperty("fileTree");
        var files = treePayload.GetProperty("files").EnumerateArray().ToList();
        Assert.NotEmpty(files);

        var search = await _fixture.Client.CallToolAsync(
            "search_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["pattern"] = nameof(McpCodeGraphServer),
                ["maxResults"] = 3,
                ["maxResponseBytes"] = 16_384,
            });
        Assert.False(search.IsError == true, string.Join("\n", search.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.NotNull(search.StructuredContent);
        var searchPayload = search.StructuredContent!.Value.GetProperty("assemblySearch");
        AssertPublicAssemblyWire(search);
        Assert.Equal("text", searchPayload.GetProperty("searchKind").GetString());
        Assert.NotEmpty(searchPayload.GetProperty("results").EnumerateArray());
        var completeness = searchPayload.GetProperty("completeness").GetString();
        Assert.Contains(completeness, new[] { "complete", "truncated", "partial" });
        if (completeness == "truncated")
        {
            Assert.True(searchPayload.TryGetProperty("continuationToken", out var continuation));
            Assert.False(string.IsNullOrWhiteSpace(continuation.GetString()));
        }
        Assert.All(
            searchPayload.GetProperty("results").EnumerateArray(),
            result =>
            {
                Assert.StartsWith("asm-search:", result.GetProperty("id").GetString(), StringComparison.Ordinal);
                Assert.DoesNotContain("\\", result.GetProperty("filePath").GetString(), StringComparison.Ordinal);
            });

        var dataAccess = await _fixture.Client.CallToolAsync(
            "search_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["searchKind"] = "data_access",
                ["maxResults"] = 2,
            });
        Assert.False(dataAccess.IsError == true, string.Join("\n", dataAccess.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.Equal(
            "data_access",
            dataAccess.StructuredContent!.Value.GetProperty("assemblySearch").GetProperty("searchKind").GetString());
        AssertPublicAssemblyWire(dataAccess);
    }

    [Fact]
    public async Task GetServerHealth_UsesAggregateProjectAndAssemblyTargetVariants()
    {
        var host = await _fixture.GetHostAsync();
        var aggregate = await _fixture.Client.CallToolAsync("get_server_health");
        Assert.NotEqual(true, aggregate.IsError);
        Assert.NotNull(aggregate.StructuredContent);
        Assert.False(aggregate.StructuredContent!.Value.GetProperty("sessionsIncluded").GetBoolean());
        Assert.Equal(0, aggregate.StructuredContent.Value.GetProperty("shownSessionCount").GetInt32());
        Assert.False(aggregate.StructuredContent.Value.TryGetProperty("assemblies", out _));
        var aggregateText = Assert.IsType<TextContentBlock>(Assert.Single(aggregate.Content)).Text;
        Assert.DoesNotContain("includeSessions", aggregateText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Generation", aggregateText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GeneratedPath", aggregateText, StringComparison.OrdinalIgnoreCase);

        await _fixture.Client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["targetPath"] = host.TargetPath,
                ["namePatterns"] = new[] { "Greeter" },
            });

        var project = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?>
            {
                ["targetPath"] = host.TargetPath,
            });
        Assert.False(
            project.IsError == true,
            string.Join("\n", project.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.Contains(host.TargetPath, Assert.IsType<TextContentBlock>(Assert.Single(project.Content)).Text, StringComparison.OrdinalIgnoreCase);

        var assembly = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
            });
        Assert.False(
            assembly.IsError == true,
            string.Join("\n", assembly.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        var assemblyText = Assert.IsType<TextContentBlock>(Assert.Single(assembly.Content)).Text;
        Assert.Contains("Assembly-Sessions (1)", assemblyText, StringComparison.Ordinal);
        Assert.Contains("Origin:", assemblyText, StringComparison.Ordinal);
        Assert.DoesNotContain("Generation", assemblyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GeneratedPath", assemblyText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(assembly.StructuredContent);
        Assert.True(assembly.StructuredContent!.Value.GetProperty("sessionsIncluded").GetBoolean());
        Assert.Equal(1, assembly.StructuredContent.Value.GetProperty("shownSessionCount").GetInt32());
        Assert.Single(assembly.StructuredContent!.Value.GetProperty("assemblies").EnumerateArray());
    }

    [Fact]
    public async Task InspectAssembly_RegistrationAdvertisesGenericFiltersAndParameterMetadata()
    {
        var tool = Assert.Single((await _fixture.Client.ListToolsAsync())
            .Where(candidate => candidate.ProtocolTool.Name == "inspect_assembly"));
        var schema = tool.ProtocolTool.InputSchema.ToString();

        Assert.Contains("exactTypeName", schema, StringComparison.Ordinal);
        Assert.Contains("memberNames", schema, StringComparison.Ordinal);
        Assert.Contains("maxMembers", schema, StringComparison.Ordinal);
        Assert.Contains("strukturierte Parameterdaten", tool.ProtocolTool.Description, StringComparison.Ordinal);

        var healthTool = Assert.Single((await _fixture.Client.ListToolsAsync())
            .Where(candidate => candidate.ProtocolTool.Name == "get_server_health"));
        Assert.Contains("includeDiagnostics", healthTool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("maxDiagnostics", healthTool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("includeSessions", healthTool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("maxSessions", healthTool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);

        var searchTool = Assert.Single((await _fixture.Client.ListToolsAsync())
            .Where(candidate => candidate.ProtocolTool.Name == "search_assembly"));
        Assert.Contains("data_access", searchTool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("external_calls", searchTool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("continuationToken", searchTool.ProtocolTool.Description, StringComparison.Ordinal);

        foreach (var name in new[] { "inspect_assembly", "find_assembly_extensions", "search_assembly", "get_assembly_context" })
        {
            var candidate = Assert.Single((await _fixture.Client.ListToolsAsync())
                .Where(tool => tool.ProtocolTool.Name == name));
            var inputSchema = candidate.ProtocolTool.InputSchema.ToString();
            Assert.DoesNotContain("\"cursor\"", inputSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("includeSessions", inputSchema, StringComparison.Ordinal);
            Assert.DoesNotContain("isTruncated", candidate.ProtocolTool.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("Generation", candidate.ProtocolTool.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetServerHealth_WithIncludeDiagnostics_ReturnsDetailedDiagnosticsPayload()
    {
        var host = await _fixture.GetHostAsync();
        var health = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?>
            {
                ["targetPath"] = host.TargetPath,
                ["includeDiagnostics"] = true,
                ["maxDiagnostics"] = 5,
            });

        Assert.False(
            health.IsError == true,
            string.Join("\n", health.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.NotNull(health.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(health.Content)).Text;
        Assert.Contains(host.TargetPath, text, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPublicAssemblyWire(CallToolResult result)
    {
        var structured = result.StructuredContent?.GetRawText() ?? string.Empty;
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        foreach (var forbidden in new[] { "cursor", "isTruncated", "generation", "generatedPath", "decompiledProjectDirectory", "decompiledProjectPath", "decompiledSourceRoot" })
        {
            Assert.DoesNotContain(forbidden, structured, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertAssemblyNavigation(CallToolResult result, string text)
    {
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");
        Assert.Equal("decompiled", navigation.GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("operationStatus").GetString());
        Assert.Contains(
            $"operationStatus: `{navigation.GetProperty("operationStatus").GetString()}`",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            $"completeness: `{navigation.GetProperty("completeness").GetString()}`",
            text,
            StringComparison.Ordinal);
    }
}
