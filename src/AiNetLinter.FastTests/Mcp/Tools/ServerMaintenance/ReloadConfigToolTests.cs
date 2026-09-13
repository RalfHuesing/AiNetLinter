#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Server;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.TestKit;
using Xunit;
using static AiNetLinter.TestKit.McpTestResultText;

namespace AiNetLinter.FastTests.Mcp.Tools.ServerMaintenance;

[Trait("Category", "Unit")]
public sealed class ReloadConfigToolTests
{
    [Fact]
    public async Task MissingOptionalRulesWithoutActiveConfig_ReturnsNoOpAndRefreshesSolution()
    {
        using var tempDir = TestTempDirectory.Create("reload-config-missing-");
        var solutionPath = ProjectRegistryFixture.CreateProjectRoot(
            tempDir,
            "reload-config",
            rulesRelative: string.Empty);
        var server = CreateServer(config: null, resolvedConfigPath: null);
        await TestWaiter.WaitForConditionAsync(
            () => server.LoadState == ServerLoadState.Loaded,
            TimeSpan.FromSeconds(15));
        Assert.NotNull(server.GetCurrentSolution());
        var refreshCountBefore = server.RefreshCount;

        var result = await ReloadConfigTool.ExecuteAsync(
            server,
            RulesPath(solutionPath),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("operation=ok", text, StringComparison.Ordinal);
        Assert.Contains("configState=not_configured", text, StringComparison.Ordinal);
        Assert.Equal(refreshCountBefore + 1, server.RefreshCount);
    }

    [Fact]
    public async Task MissingRulesWithActiveConfig_ReturnsConfigNotFoundWithoutRefresh()
    {
        using var tempDir = TestTempDirectory.Create("reload-config-active-");
        var solutionPath = ProjectRegistryFixture.CreateProjectRoot(
            tempDir,
            "reload-config",
            rulesRelative: string.Empty);
        var config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
        var server = CreateServer(config, "previous-rules.json");
        await TestWaiter.WaitForConditionAsync(
            () => server.LoadState == ServerLoadState.Loaded,
            TimeSpan.FromSeconds(15));
        Assert.NotNull(server.GetCurrentSolution());
        var refreshCountBefore = server.RefreshCount;

        var result = await ReloadConfigTool.ExecuteAsync(
            server,
            RulesPath(solutionPath),
            CancellationToken.None);

        Assert.Contains("[ERROR]: CONFIG_NOT_FOUND", TextOf(result), StringComparison.Ordinal);
        Assert.Same(config, server.Config);
        Assert.Equal(refreshCountBefore, server.RefreshCount);
    }

    private static McpCodeGraphServer CreateServer(Config? config, string? resolvedConfigPath) =>
        new(new McpCodeGraphServerOptions
        {
            Catalog = CreateCatalog(),
            Console = new RecordingLintConsole(),
            Config = config,
            ResolvedConfigPath = resolvedConfigPath,
            LoadFunc = _ => Task.FromResult<SourceFileCatalog?>(CreateCatalog()),
        });

    private static SourceFileCatalog CreateCatalog() =>
        new(SymbolGraphMiniSolutionSpec.Create().Solution, hasLoadingErrors: false);

    private static string RulesPath(string solutionPath) =>
        Path.Combine(solutionPath, "ainetlinter-rules.json");
}
