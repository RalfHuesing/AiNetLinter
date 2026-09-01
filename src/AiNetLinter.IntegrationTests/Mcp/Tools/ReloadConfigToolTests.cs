#nullable enable

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="ReloadConfigTool"/>. Jeder Test nutzt eine frische
/// <see cref="SymbolGraphMiniFixtureWorkspace"/>-Kopie statt einer geteilten Fixture, weil die
/// Tests rules.json-Dateien auf der Platte schreiben und die Server-Config zur Laufzeit mutieren.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReloadConfigToolTests
{
    private static Config CreateConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await ReloadConfigTool.ExecuteAsync(state, string.Empty, null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitConfigPathDoesNotExist_ReturnsRecoverableConfigNotFound_OldConfigStaysActive()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig, UsedDefaultConfig: true)));

        var missingPath = Path.Combine(fixture.RootPath, "does-not-exist.json");

        var result = await ReloadConfigTool.ExecuteAsync(state, string.Empty, missingPath, CancellationToken.None);

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
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig, UsedDefaultConfig: true)));

        var invalidPath = Path.Combine(fixture.RootPath, "broken-rules.json");
        await File.WriteAllTextAsync(invalidPath, "{ this is not valid json ");

        var result = await ReloadConfigTool.ExecuteAsync(state, string.Empty, invalidPath, CancellationToken.None);

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
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        var newPath = Path.Combine(fixture.RootPath, "new-rules.json");
        await File.WriteAllTextAsync(newPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, string.Empty, newPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Config neu geladen", text);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ReloadConfigPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(newPath, payload!.ConfigPath);
        Assert.Equal(15, payload.PreviousEnabledRuleCount);
        Assert.Equal(14, payload.EnabledRuleCount);
        Assert.Equal(-1, payload.EnabledRuleDelta);
        Assert.False(state.UsedDefaultConfig);
        Assert.Equal(newPath, state.ResolvedConfigPath);
        Assert.False(state.Config.Global.BanAsyncVoid);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPath_ReloadsPreviouslyResolvedPathPickingUpDiskChange()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var existingPath = Path.Combine(fixture.RootPath, "rules.json");
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": {}, \"Metrics\": {} }");
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                catalog, Config: CreateConfig(), UsedDefaultConfig: false, ResolvedConfigPath: existingPath)));

        // Nutzer aendert die rules.json waehrend der Server laeuft.
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, existingPath, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.False(state.Config.Global.BanAsyncVoid);
        Assert.Equal(existingPath, state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPathAndNoneDiscoverable_ReturnsInformationalTextWithoutError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        var result = await ReloadConfigTool.ExecuteAsync(
            state,
            Path.Combine(fixture.RootPath, "missing-rules.json"),
            null,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_NOT_FOUND", text);
        Assert.True(state.UsedDefaultConfig);
    }

    [Fact]
    public async Task ExecuteAsync_NoConfigPath_DoesNotSearchNextToSolution()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        // Eine benachbarte rules.json ist kein Fallback fuer den Definitionspfad.
        var discoveredPath = Path.Combine(fixture.RootPath, "rules.json");
        await File.WriteAllTextAsync(discoveredPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(
            state,
            Path.Combine(fixture.RootPath, "missing-rules.json"),
            null,
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("CONFIG_NOT_FOUND", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.True(state.UsedDefaultConfig);
        Assert.Null(state.ResolvedConfigPath);
        Assert.True(state.Config.Global.BanAsyncVoid);
    }

    [Fact]
    public async Task ExecuteAsync_WithLoadedSolution_ReloadsSolutionWorkspaceAndIncrementsRefreshCount()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig(), UsedDefaultConfig: true)));

        var initialRefreshCount = state.RefreshCount;

        var rulesPath = Path.Combine(fixture.RootPath, "rules.json");
        await File.WriteAllTextAsync(rulesPath, "{ \"Global\": {}, \"Metrics\": {} }");
        var result = await ReloadConfigTool.ExecuteAsync(state, rulesPath, null, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.True(state.RefreshCount > initialRefreshCount);
        Assert.NotNull(state.GetCurrentSolution());
    }
}
