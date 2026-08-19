#nullable enable

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="GetServerHealthTool"/>: LoadState/Solution/Config-Anzeige,
/// Uptime/Refresh-Aggregate und die Call-Log-Aggregation (aktiv vs. nicht aktiv).
/// </summary>
[Trait("Category", "Integration")]
public sealed class GetServerHealthToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetServerHealthToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_LoadFailed_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetServerHealthTool.ExecuteAsync(state, observabilityLogPath: null);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_ReportsLoadStateSolutionAndUptime()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetServerHealthTool.ExecuteAsync(state, observabilityLogPath: null);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Version:", text);
        Assert.Contains("Loaded", text);
        Assert.Contains(_fixture.RootPath, text, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Uptime", text);
        Assert.Contains("Solution-Refreshes seit Start: 0", text);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_StructuredContentDeserializesToServerHealthPayload()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetServerHealthTool.ExecuteAsync(state, observabilityLogPath: null);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Version));
        Assert.Equal("Loaded", payload.LoadState);
        Assert.Equal(0, payload.RefreshCount);
        Assert.Null(payload.CallLog);
    }

    [Fact]
    public async Task ExecuteAsync_UsedDefaultConfig_MentionsDefaultRules()
    {
        using var state = _fixture.CreateReadOnlyServer(usedDefaultConfig: true);

        var result = await GetServerHealthTool.ExecuteAsync(state, observabilityLogPath: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Default-Regeln", text);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultObservability_ReportsObservabilityActive()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await GetServerHealthTool.ExecuteAsync(state, observabilityLogPath: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv", text);
    }

    [Fact]
    public async Task ExecuteAsync_CustomObservabilityPath_ReportsCustomLogPath()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var customPath = "C:\\Custom\\Logs";
        var result = await GetServerHealthTool.ExecuteAsync(state, customPath);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv (C:\\Custom\\Logs)", text);
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityService_ReportsActiveWithLogPath()
    {
        using var state = _fixture.CreateReadOnlyServer();
        var obsService = new FakeObservabilityService(isEnabled: true, logFilePath: "C:\\Logs\\AiNetLinter_123.jsonl");

        var result = await GetServerHealthTool.ExecuteAsync(state, obsService);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv (C:\\Logs\\AiNetLinter_123.jsonl)", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.CallLog);
        Assert.Equal("C:\\Logs\\AiNetLinter_123.jsonl", payload.CallLog!.LogPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityServiceDisabled_ReportsDisabled()
    {
        using var state = _fixture.CreateReadOnlyServer();
        var obsService = new FakeObservabilityService(isEnabled: false);

        var result = await GetServerHealthTool.ExecuteAsync(state, obsService);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: deaktiviert.", text);
    }

    private sealed class FakeObservabilityService(bool isEnabled, string? logFilePath = null) : RalfHuesing.Mcp.Observability.IMcpObservabilityService
    {
        public bool IsEnabled => isEnabled;
        public string ServerName => "ainetlinter";
        public string ServerVersion => "1.0.96";
        public string? CurrentLogFilePath => logFilePath;
        public string? CurrentFeedbackLogFilePath => null;
        public int ProcessId => 12345;
        public string InstanceId => "fake-instance-id";
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
