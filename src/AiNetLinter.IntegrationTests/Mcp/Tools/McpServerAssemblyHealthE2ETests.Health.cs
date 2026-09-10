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

public sealed partial class McpServerAssemblyHealthE2ETests
{
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
        var assemblyNavigation = assembly.StructuredContent.Value.GetProperty("navigation");
        Assert.Equal("assembly", assemblyNavigation.GetProperty("snapshot").GetProperty("kind").GetString());
        Assert.True(assemblyNavigation.GetProperty("snapshot").GetProperty("fresh").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(assemblyNavigation.GetProperty("snapshot").GetProperty("fingerprint").GetString()));

        var followUp = await _fixture.Client.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = typeof(McpCodeGraphServer).Assembly.Location,
                ["typeName"] = nameof(McpCodeGraphServer),
                ["exactTypeName"] = true,
                ["publicOnly"] = false,
                ["maxMembers"] = 1,
            });
        Assert.False(followUp.IsError == true, followUp.ToString());
        Assert.Equal(
            assemblyNavigation.GetProperty("snapshot").GetProperty("fingerprint").GetString(),
            followUp.StructuredContent!.Value.GetProperty("navigation").GetProperty("snapshot").GetProperty("fingerprint").GetString());

        var globalDetail = await _fixture.Client.CallToolAsync(
            "get_server_health",
            new Dictionary<string, object?>
            {
                ["includeDiagnostics"] = true,
                ["maxDiagnostics"] = 1,
            });
        Assert.False(globalDetail.IsError == true, globalDetail.ToString());
        var globalPayload = globalDetail.StructuredContent!.Value;
        Assert.False(globalPayload.GetProperty("diagnosticsIncluded").GetBoolean());
        Assert.False(globalPayload.GetProperty("sessionsIncluded").GetBoolean());
        Assert.False(globalPayload.TryGetProperty("assemblies", out _));
        Assert.DoesNotContain("Origin:", Assert.IsType<TextContentBlock>(Assert.Single(globalDetail.Content)).Text, StringComparison.Ordinal);
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
}
