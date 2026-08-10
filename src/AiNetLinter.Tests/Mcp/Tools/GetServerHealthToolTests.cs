#nullable enable

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="GetServerHealthTool"/> (Q3, <c>tasks/features/05-roadmap.md</c> §3):
/// LoadState/Solution/Config-Anzeige, Uptime/Refresh-Aggregate und die Call-Log-Aggregation
/// (aktiv vs. nicht aktiv).
/// </summary>
[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
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

        var result = await GetServerHealthTool.ExecuteAsync(state, callLog: null);

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("SOLUTION_NOT_LOADED", text);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_ReportsLoadStateSolutionAndUptime()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetServerHealthTool.ExecuteAsync(state, callLog: null);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Loaded", text);
        Assert.Contains(_fixture.Workspace.RootPath, text, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Uptime", text);
        Assert.Contains("Solution-Refreshes seit Start: 0", text);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_StructuredContentDeserializesToServerHealthPayload()
    {
        // S1.3: StructuredContent ergaenzt den Text additiv — dieselben Rohwerte wie die
        // Text-Zeilen "LoadState"/"Solution-Refreshes seit Start" oben.
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetServerHealthTool.ExecuteAsync(state, callLog: null);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("Loaded", payload!.LoadState);
        Assert.Equal(0, payload.RefreshCount);
        Assert.Null(payload.CallLog);
    }

    [Fact]
    public async Task ExecuteAsync_UsedDefaultConfig_MentionsDefaultRules()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog, UsedDefaultConfig: true)));

        var result = await GetServerHealthTool.ExecuteAsync(state, callLog: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Default-Regeln", text);
    }

    [Fact]
    public async Task ExecuteAsync_NoCallLog_ReportsCallLogNotActive()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetServerHealthTool.ExecuteAsync(state, callLog: null);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Call-Log: nicht aktiv", text);
    }

    [Fact]
    public async Task ExecuteAsync_ActiveCallLogWithRecordedCalls_ReportsAggregatesPerTool()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var logPath = CreateTempLogPath();
        try
        {
            await using var log = new McpCallLog(logPath);
            await log.ExecuteCallAsync("find_symbol", "Greeter",
                () => Task.FromResult(McpToolResults.Text("hit")));
            await log.ExecuteCallAsync("find_symbol", "Caller",
                () => Task.FromResult(McpToolResults.Text("hit")));
            await log.ExecuteCallAsync("get_violations", "",
                () => Task.FromResult(McpToolResults.SolutionNotLoaded()));

            var result = await GetServerHealthTool.ExecuteAsync(state, log);

            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
            Assert.Contains("Call-Log: aktiv", text);
            Assert.Contains("Eintraege gesamt: 3, Fehler: 1", text);
            Assert.Contains("find_symbol: 2", text);
            Assert.Contains("get_violations: 1", text);
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    private static string CreateTempLogPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mcp-server-health-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "calls.log");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup, kein Test-Fail
        }
    }
}
