#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpServerCommandContractTests
{
    private readonly ReadOnlyMcpHostFixture fixture;

    public McpServerCommandContractTests(ReadOnlyMcpHostFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing()
    {
        var path = CreateTempDir();
        try
        {
            var slnx = Path.Combine(path, "Broken.slnx");
            File.WriteAllText(slnx, "<this-is-not-a-valid-slnx-document>");
            var console = new RecordingLintConsole();
            var exception = await Record.ExceptionAsync(async () =>
                await McpServerCommand.TryLoadSolutionAsync(slnx, CancellationToken.None, console));

            Assert.Null(exception);
            Assert.Contains(console.ErrorLines, line => line.Contains("[WARN]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault()
    {
        var path = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(path, "Only.slnx"), "");
            var args = new LinterArgs { ConfigPath = null, TargetPath = path, Verbose = false };

            Assert.NotNull(McpServerCommand.ResolveConfig(args, null));
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task RunAsync_ValidFixture_ServerRespondsWithAllTools()
    {
        var tools = await (await fixture.GetHostAsync()).ListToolsAsync();
        Assert.Equal(26, tools.Count);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture() =>
        await AssertTextAsync("get_hotspots", new Dictionary<string, object?>(), "im gruenen Bereich");

    [Fact]
    public async Task RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown() =>
        await AssertTextAsync("get_index_scope", new Dictionary<string, object?>(), ".cs:");

    [Fact]
    public async Task RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation() =>
        await AssertTextAsync("get_violations", new Dictionary<string, object?>(), "ViolationTrigger");

    [Fact]
    public async Task RunAsync_ValidFixture_SearchPatternReturnsExpectedHit() =>
        await AssertTextAsync("search_pattern", new Dictionary<string, object?> { ["pattern"] = "Greeter" }, "Greeter.cs");

    [Fact]
    public async Task RunAsync_ValidFixture_SearchPatternStructuredArgumentsBind()
    {
        var result = await (await fixture.GetHostAsync()).CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "Greeter",
                ["maxFiles"] = 1,
                ["contextLines"] = 1,
                ["maxResponseBytes"] = 4096,
                ["scope"] = "src",
                ["includePatterns"] = new[] { "**/*.cs" },
                ["enrichCSharp"] = true,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal(
            System.Text.Json.JsonValueKind.Object,
            result.StructuredContent!.Value.ValueKind);
        var matches = result.StructuredContent.Value.GetProperty("matches").EnumerateArray().ToArray();
        Assert.NotEmpty(matches);
        Assert.Contains(matches, match => match.TryGetProperty("semantic", out var semantic)
            && semantic.ValueKind == System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task SearchPatternRegistration_AdvertisesOptInEnrichment()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tool = McpServerOptionsFactory.Create(registry).ToolCollection!
            .Single(candidate => candidate.ProtocolTool.Name == "search_pattern");

        Assert.Contains("enrichCSharp", tool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("enrichCSharp=true", tool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("ambiguous", tool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("unavailable", tool.ProtocolTool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolReturnsMatch() =>
        await AssertTextAsync("find_symbol", new Dictionary<string, object?> { ["namePattern"] = "Greeter" }, "Greeter");

    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesReturnsCallSite() =>
        await AssertTextAsync("find_references", new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet" }, "Caller.cs");

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        workspace.CommitCalculatorAddBodyChange();
        await using var host = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));
        var text = await host.CallToolGetTextAsync("get_impact", new Dictionary<string, object?> { ["gitRef"] = "HEAD~1" });
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        workspace.ChangeCalculatorAddBodyWithoutCommitting();
        await using var host = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));
        var text = await host.CallToolGetTextAsync("get_impact", new Dictionary<string, object?>());
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature() =>
        await AssertTextAsync("get_file_skeleton", new Dictionary<string, object?> { ["filePath"] = "src/SymbolGraphMini/Greeter.cs" }, "Greet");

    [Fact]
    public async Task RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy() =>
        await AssertTextAsync("get_type_hierarchy", new Dictionary<string, object?> { ["symbolIdentifier"] = "BaseGreeting" }, "IGreeting");

    private async Task AssertTextAsync(string tool, IReadOnlyDictionary<string, object?> arguments, string expected)
    {
        var result = await (await fixture.GetHostAsync()).CallToolAsync(tool, arguments);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(expected, text, StringComparison.Ordinal);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(TestTempDirectory.RootTempDirectory, $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
