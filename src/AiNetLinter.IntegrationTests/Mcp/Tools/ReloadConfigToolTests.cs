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
/// Tests ainetlinter-rules.json-Dateien auf der Platte schreiben und die Server-Config zur Laufzeit mutieren.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReloadConfigToolTests
{
    private static Config CreateConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var tempDir = TestTempDirectory.Create("mcp-reload-unloaded-");
        tempDir.CreateFile("app.slnx", string.Empty);
        var rulesPath = tempDir.CreateFile("ainetlinter-rules.json", "{}");
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await ReloadConfigTool.ExecuteAsync(state, rulesPath, CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_NeighborRulesPathDoesNotExist_ReturnsRecoverableConfigNotFound_OldConfigStaysActive()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig)));

        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var missingPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");

        var result = await ReloadConfigTool.ExecuteAsync(state, missingPath, CancellationToken.None);

        // isError-Policy: eine fehlende ainetlinter-rules.json ist ein behebbarer Pfadfehler (Tippfehler,
        // falscher Ordner) — IsError bleibt false, siehe IsErrorPolicy.md.
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_NOT_FOUND", text);

        // Bisherige Config bleibt unveraendert aktiv - kein Datenverlust, kein Absturz.
        Assert.Same(originalConfig, state.Config);
        Assert.Null(state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_NeighborRulesPathInvalidJson_ReturnsRecoverableConfigInvalid_OldConfigStaysActive()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var originalConfig = CreateConfig();
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: originalConfig)));

        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var invalidPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");
        await File.WriteAllTextAsync(invalidPath, "{ this is not valid json ");

        var result = await ReloadConfigTool.ExecuteAsync(state, invalidPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_INVALID", text);

        // Bisherige Config bleibt unveraendert aktiv - kein Datenverlust, kein Absturz.
        Assert.Same(originalConfig, state.Config);
        Assert.Null(state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_ValidNeighborRulesPath_ReplacesConfigAndReportsSummary()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig())));

        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var newPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");
        await File.WriteAllTextAsync(newPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, newPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Config neu geladen", text);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ReloadConfigPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(newPath, payload!.ConfigPath);
        Assert.Equal(17, payload.PreviousEnabledRuleCount);
        Assert.Equal(16, payload.EnabledRuleCount);
        Assert.Equal(-1, payload.EnabledRuleDelta);
        Assert.Equal(newPath, state.ResolvedConfigPath);
        Assert.False(state.Config!.Global.BanAsyncVoid);
    }

    [Fact]
    public async Task ExecuteAsync_ResolvedNeighborRulesPath_PicksUpDiskChange()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var existingPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": {}, \"Metrics\": {} }");
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                catalog, Config: CreateConfig(), ResolvedConfigPath: existingPath)));

        // Nutzer aendert die ainetlinter-rules.json waehrend der Server laeuft.
        await File.WriteAllTextAsync(existingPath, "{ \"Global\": { \"BanAsyncVoid\": false }, \"Metrics\": {} }");

        var result = await ReloadConfigTool.ExecuteAsync(state, existingPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.False(state.Config!.Global.BanAsyncVoid);
        Assert.Equal(existingPath, state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_MissingNeighborRules_ReturnsInformationalTextWithoutError()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig())));

        var result = await ReloadConfigTool.ExecuteAsync(
            state,
            Path.Combine(fixture.RootPath, "ainetlinter-rules.json"),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CONFIG_NOT_FOUND", text);
        Assert.Null(state.ResolvedConfigPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithLoadedSolution_ReloadsSolutionWorkspaceAndIncrementsRefreshCount()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var catalog = await LoadedFixture.LoadCatalogAsync(fixture.RootPath);
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog, Config: CreateConfig())));

        var initialRefreshCount = state.RefreshCount;

        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var rulesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");
        await File.WriteAllTextAsync(rulesPath, "{ \"Global\": {}, \"Metrics\": {} }");
        var result = await ReloadConfigTool.ExecuteAsync(state, rulesPath, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.True(state.RefreshCount > initialRefreshCount);
        Assert.NotNull(state.GetCurrentSolution());
    }
}
