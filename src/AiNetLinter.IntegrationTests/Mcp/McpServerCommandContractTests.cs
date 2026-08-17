#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
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
            var console = new RecordingLintConsole();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Null(McpServerCommand.TryResolveRulesJsonPath(null, Path.Combine(path, "Only.slnx")));
            Assert.NotNull(McpServerCommand.ResolveConfig(args, null));
            await Record.ExceptionAsync(() => McpServerCommand.RunAsync(args, cancellation.Token, console));
            Assert.Contains(console.ErrorLines, line => line.Contains("Keine rules.json", StringComparison.Ordinal));
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task RunAsync_ValidFixture_ServerRespondsWithAllTools()
    {
        var tools = await (await fixture.GetHostAsync()).ListToolsAsync();
        Assert.Equal(22, tools.Count);
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
        await AssertTextAsync("get_type_hierarchy", new Dictionary<string, object?> { ["typeIdentifier"] = "BaseGreeting" }, "IGreeting");

    private async Task AssertTextAsync(string tool, IReadOnlyDictionary<string, object?> arguments, string expected)
    {
        var result = await (await fixture.GetHostAsync()).CallToolAsync(tool, arguments);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(expected, text, StringComparison.Ordinal);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
