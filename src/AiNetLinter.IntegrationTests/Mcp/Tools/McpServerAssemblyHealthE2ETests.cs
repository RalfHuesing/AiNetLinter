#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Validation;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// E2E-Vertraege fuer Assembly-Analyse und die projekt-/assemblybezogene Health-Sicht.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class McpServerAssemblyHealthE2ETests
{
    private readonly ReadOnlyMcpHostFixture _fixture;

    public McpServerAssemblyHealthE2ETests(ReadOnlyMcpHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("maxBodyLines")]
    [InlineData("maxCallers")]
    [InlineData("depth")]
    [InlineData("topN")]
    public async Task GetAssemblyContext_ZeroPositiveLimitReturnsFieldAwareInvalidArgument(string fieldName)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_assembly_context",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                [fieldName] = 0,
            });

        Assert.False(result.IsError == true, result.ToString());
        Assert.NotNull(result.StructuredContent);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal($"$.{fieldName}", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
        Assert.Contains("mindestens 1", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("maxBodyLines", 1001)]
    [InlineData("maxCallers", 201)]
    [InlineData("depth", 4)]
    [InlineData("topN", 201)]
    public async Task GetAssemblyContext_UpperLimitReturnsFieldAwareInvalidArgument(string fieldName, int value)
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_assembly_context",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                [fieldName] = value,
            });

        Assert.False(result.IsError == true, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal($"$.{fieldName}", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
        Assert.Contains("höchstens", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyCallTree_InvalidFormatReturnsRecoverableInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_call_tree",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["symbolIdentifier"] = nameof(McpCodeGraphServer),
                ["format"] = "json",
            });

        Assert.True(result.IsError, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.format", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task AssemblyDiscovery_NegativeResponseBudgetIsRejectedOnAssemblyRoute()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["maxResponseBytes"] = -1,
            });

        Assert.False(result.IsError == true, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxResponseBytes", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task AssemblyDiscovery_CappedNamespaceTreePreservesNavigationAndWireEnvelope()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_namespace_tree",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["maxResults"] = 200,
                ["maxResponseBytes"] = 4096,
            });

        Assert.False(result.IsError == true, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.True(payload.TryGetProperty("navigation", out var navigation), payload.GetRawText());
        Assert.Equal("assembly", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.True(payload.TryGetProperty("analysis", out var analysis), payload.GetRawText());
        Assert.False(string.IsNullOrWhiteSpace(analysis.GetProperty("assemblyHash").GetString()));
        Assert.True(McpResponseSize.From(result).TotalBytes <= 4096, payload.GetRawText());
        Assert.Contains("[ASSEMBLY]", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyDiscovery_CappedClassStructurePreservesNavigationAndWireEnvelope()
    {
        var result = await _fixture.Client.CallToolAsync(
            "get_class_structure",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["symbolIdentifier"] = nameof(McpCodeGraphServer),
                ["maxMembers"] = 200,
                ["maxResponseBytes"] = 4096,
            });

        Assert.False(result.IsError == true, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        var payload = result.StructuredContent!.Value;
        Assert.Equal("assembly", payload.GetProperty("navigation").GetProperty("target").GetProperty("origin").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("analysis").GetProperty("assemblyHash").GetString()));
        Assert.Contains("[ASSEMBLY]", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyDiscovery_CappedFileSkeletonPreservesNavigationEnvelope()
    {
        var tree = await _fixture.Client.CallToolAsync(
            "get_file_tree",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["view"] = "files",
                ["includeExtensions"] = new[] { ".cs" },
                ["maxResults"] = 1,
            });
        var filePath = tree.StructuredContent!.Value.GetProperty("fileTree").GetProperty("files")[0].GetProperty("path").GetString();
        Assert.False(string.IsNullOrWhiteSpace(filePath), tree.StructuredContent.Value.GetRawText());

        var result = await _fixture.Client.CallToolAsync(
            "get_file_skeleton",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["filePaths"] = new[] { filePath },
                ["maxResponseBytes"] = 4096,
            });

        Assert.False(result.IsError == true, string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        Assert.Equal("assembly", result.StructuredContent!.Value.GetProperty("navigation").GetProperty("target").GetProperty("origin").GetString());
        Assert.Contains("[ASSEMBLY]", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssemblyRoute_ArrayElementTypeMismatchReturnsIndexedFieldAwareInvalidArgument()
    {
        var result = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["memberNames"] = new object?[] { "Dispose", 42 },
            });

        Assert.False(result.IsError == true, result.ToString());
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.memberNames[1]", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task AssemblyDetailLevel_InvalidValueReturnsBeforeAnalysis_AndValidRetrySucceeds()
    {
        var invalid = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["detailLevel"] = "verbose",
            });

        Assert.Equal("INVALID_ARGUMENT", invalid.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.detailLevel", invalid.StructuredContent.Value.GetProperty("fieldPath").GetString());
        Assert.False(invalid.StructuredContent.Value.TryGetProperty("types", out _), invalid.StructuredContent.Value.GetRawText());
        Assert.False(invalid.StructuredContent.Value.TryGetProperty("analysis", out _), invalid.StructuredContent.Value.GetRawText());

        var retry = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["detailLevel"] = "standard",
                ["maxResults"] = 1,
            });

        Assert.False(retry.IsError == true, retry.ToString());
        Assert.True(retry.StructuredContent!.Value.TryGetProperty("types", out _), retry.StructuredContent.Value.GetRawText());
    }

    [Theory]
    [InlineData("find_assembly_extensions")]
    [InlineData("get_assembly_context")]
    public async Task AssemblyDetailLevel_OtherAssemblyToolsRejectInvalidValue_AndAllowRetry(string toolName)
    {
        var targetPath = typeof(McpCodeGraphServer).Assembly.Location;
        var invalid = await _fixture.Client.CallToolAsync(
            toolName,
            new Dictionary<string, object?>
            {
                ["targetPath"] = targetPath,
                ["detailLevel"] = "verbose",
            });

        Assert.Equal("INVALID_ARGUMENT", invalid.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.detailLevel", invalid.StructuredContent.Value.GetProperty("fieldPath").GetString());
        Assert.False(invalid.StructuredContent.Value.TryGetProperty("analysis", out _), invalid.StructuredContent.Value.GetRawText());

        var retry = await _fixture.Client.CallToolAsync(
            toolName,
            new Dictionary<string, object?>
            {
                ["targetPath"] = targetPath,
                ["detailLevel"] = McpEnumValues.AssemblyDetailLevelStandard,
                ["maxResults"] = 1,
            });

        Assert.False(retry.IsError == true, retry.ToString());
        Assert.NotNull(retry.StructuredContent);
    }

    [Fact]
    public async Task AssemblyDetailLevelContract_UsesCanonicalValuesForToolsAndValidator()
    {
        var expectedDescription = $"detailLevel ({McpEnumValues.AssemblyDetailLevelsHint})";
        var tools = await _fixture.Client.ListToolsAsync();
        foreach (var toolName in new[] { "inspect_assembly", "find_assembly_extensions", "get_assembly_context" })
        {
            var tool = Assert.Single(tools.Where(candidate => candidate.ProtocolTool.Name == toolName));
            Assert.Contains(expectedDescription, tool.ProtocolTool.Description, StringComparison.Ordinal);
        }

        foreach (var detailLevel in McpEnumValues.AssemblyDetailLevels)
        {
            Assert.Null(AssemblyAnalysisResponseLimits.ValidateDetailLevel(detailLevel));
        }

        Assert.NotNull(AssemblyAnalysisResponseLimits.ValidateDetailLevel("verbose"));
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
                ["maxResponseBytes"] = 12_000
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
        Assert.Equal("assembly", navigation.GetProperty("target").GetProperty("origin").GetString());
        Assert.Equal("ok", navigation.GetProperty("status").GetProperty("operation").GetString());
    }
}
