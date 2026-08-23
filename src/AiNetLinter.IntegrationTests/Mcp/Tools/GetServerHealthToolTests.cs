#nullable enable

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.Observability;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
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
        using var tempDir = TestTempDirectory.Create("mcp-health-unloaded-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "unloaded");
        await using var registry = CreateRegistry(root, new McpCodeGraphServer(
            McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null))));

        var result = await GetServerHealthTool.ExecuteAsync(registry, projectRoot: root);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("## Projekte (1)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_ReportsLoadStateSolutionAndUptime()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());

        var result = await GetServerHealthTool.ExecuteAsync(registry, observabilityLogPath: null);

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
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());

        var result = await GetServerHealthTool.ExecuteAsync(registry, observabilityLogPath: null);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Version));
        Assert.Equal("Loaded", Assert.Single(payload.Projects).LoadState);
        Assert.Equal(0, Assert.Single(payload.Projects).RefreshCount);
        Assert.Null(payload.CallLog);
    }

    [Fact]
    public async Task ExecuteAsync_UsedDefaultConfig_MentionsDefaultRules()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer(usedDefaultConfig: true));

        var result = await GetServerHealthTool.ExecuteAsync(registry, observabilityLogPath: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Default-Regeln", text);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultObservability_ReportsObservabilityActive()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());

        var result = await GetServerHealthTool.ExecuteAsync(registry, observabilityLogPath: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv", text);
    }

    [Fact]
    public async Task ExecuteAsync_CustomObservabilityPath_ReportsCustomLogPath()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());

        var customPath = "C:\\Custom\\Logs";
        var result = await GetServerHealthTool.ExecuteAsync(registry, observabilityLogPath: customPath);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv (C:\\Custom\\Logs)", text);
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityService_ReportsActiveWithLogPath()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());
        var obsService = new FakeObservabilityService(isEnabled: true, logFilePath: "C:\\Logs\\AiNetLinter_123.jsonl");

        var result = await GetServerHealthTool.ExecuteAsync(registry, obsService);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: aktiv (C:\\Logs\\AiNetLinter_123.jsonl)", text);

        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.CallLog);
        Assert.Equal("C:\\Logs\\AiNetLinter_123.jsonl", payload.CallLog!.LogPath);
    }

    [Fact]
    public async Task ExecuteAsync_WithObservabilityServiceDisabled_ReportsDisabled()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());
        var obsService = new FakeObservabilityService(isEnabled: false);

        var result = await GetServerHealthTool.ExecuteAsync(registry, obsService);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Observability: deaktiviert.", text);
    }

    [Fact]
    public async Task ExecuteAsync_WithCallLog_ReportsAggregatesInTextAndStructuredContent()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer());
        using var tempDir = TestTempDirectory.Create("mcp-health-log-");
        var logPath = tempDir.CreateFile(
            "ainetlinter_123_abc.jsonl",
            "{\"recordType\":\"tool_call\",\"toolName\":\"find_symbol\",\"isErrorResult\":false,\"success\":true}");
        var obsService = new FakeObservabilityService(isEnabled: true, logFilePath: logPath);

        var result = await GetServerHealthTool.ExecuteAsync(registry, obsService);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Call-Log-Aggregate: 1 Eintraege, 0 isError-Ergebnisse", text);
        Assert.Contains("find_symbol=1", text);
        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.CallLog!.EntryCount);
        Assert.Equal(0, payload.CallLog.ErrorCount);
        Assert.Equal(1, payload.CallLog.CallCountsByTool["find_symbol"]);
        Assert.Null(payload.CallLog.AnalysisError);
    }

    private static ProjectRegistry CreateRegistry(string root, McpCodeGraphServer server)
    {
        ProjectRegistryFixture.EnsureDefinitionsFile(root);
        var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(server));
        var lease = registry.Lease(root);
        Assert.True(lease.Succeeded);
        lease.Lease!.Dispose();
        return registry;
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
