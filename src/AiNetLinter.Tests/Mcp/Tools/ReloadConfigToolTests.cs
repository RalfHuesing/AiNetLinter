#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="ReloadConfigTool"/> (Q2, <c>tasks/features/05-roadmap.md</c> §3). Jeder
/// Test nutzt eine frische <see cref="SymbolGraphMiniFixtureWorkspace"/>-Kopie statt einer
/// geteilten Fixture, weil die Tests rules.json-Dateien auf der Platte schreiben und die
/// Server-Config zur Laufzeit mutieren.
/// </summary>
public sealed class ReloadConfigToolTests
{
    private static Config CreateConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await ReloadConfigTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitConfigPathDoesNotExist_ReturnsRecoverableConfigNotFound_OldConfigStaysActive()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig, UsedDefaultConfig: true)));

        var missingPath = Path.Combine(fixture.RootPath, "does-not-exist.json");

        var result = await ReloadConfigTool.ExecuteAsync(state, missingPath, CancellationToken.None);

        // isError-Policy: eine fehlende rules.json ist ein behebbarer Pfadfehler (Tippfehler,
        // falscher Ordner) — IsError bleibt false, siehe IsErrorPolicy.md.
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_NOT_FOUND", text);

        // Bisherige Config bleibt unveraendert aktiv - kein Datenverlust, kein Absturz.
        Assert.Same(originalConfig, state.Config);
        Assert.True(state.UsedDefaultConfig);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitConfigPathInvalidJson_ReturnsRecoverableConfigInvalid_OldConfigStaysActive()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig, UsedDefaultConfig: true)));

        var invalidPath = Path.Combine(fixture.RootPath, "broken-rules.json");
        await File.WriteAllTextAsync(invalidPath, "{ this is not valid json ");

        var result = await ReloadConfigTool.ExecuteAsync(state, invalidPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_INVALID", text);

        // Bisherige Config bleibt unveraendert aktiv - kein Datenverlust, kein Absturz.
        Assert.Same(originalConfig, state.Config);
        Assert.True(state.UsedDefaultConfig);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitValidConfigPath_ReplacesConfigAndReportsSummary()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        var newPath = Path.Combine(fixture.RootPath, "new-rules.json");
        await File.WriteAllTextAsync(newPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, newPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Config neu geladen", text);
        Assert.False(state.UsedDefaultConfig);
        Assert.Equal(newPath, state.ResolvedConfigPath);
        Assert.False(state.Config.Global.BanAsyncVoid);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPath_ReloadsPreviouslyResolvedPathPickingUpDiskChange()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var existingPath = Path.Combine(fixture.RootPath, "rules.json");
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": {}, \"Metrics\": {} }");
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                catalog, Config: CreateConfig(), UsedDefaultConfig: false, ResolvedConfigPath: existingPath)));

        // Nutzer aendert die rules.json waehrend der Server laeuft.
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.False(state.Config.Global.BanAsyncVoid);
        Assert.Equal(existingPath, state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPathAndNoneDiscoverable_ReturnsInformationalTextWithoutError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        var result = await ReloadConfigTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Default-Regeln", text);
        Assert.True(state.UsedDefaultConfig);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPathButRulesJsonAppearedNextToSolution_AutoDiscoversAndReloadsIt()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        // Server lief bisher mit Default-Regeln (keine rules.json beim Start); Nutzer legt jetzt eine an.
        var discoveredPath = Path.Combine(fixture.RootPath, "rules.json");
        await File.WriteAllTextAsync(discoveredPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.False(state.UsedDefaultConfig);
        Assert.Equal(discoveredPath, state.ResolvedConfigPath);
        Assert.False(state.Config.Global.BanAsyncVoid);
    }
}
