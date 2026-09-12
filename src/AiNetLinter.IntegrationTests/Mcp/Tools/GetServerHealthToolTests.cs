#nullable enable

using System.Text.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Tests fuer <see cref="GetServerHealthTool"/>: LoadState/Solution/Config-Anzeige,
/// Uptime/Refresh-Aggregate und den strukturierten Health-Vertrag.
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
        var root = ProjectRegistryFixture.CreateProjectRoot(
            tempDir,
            "unloaded",
            rulesRelative: "ainetlinter-rules.json");
        var solutionPath = Path.Combine(root, "app.slnx");
        await using var registry = CreateRegistry(solutionPath, new McpCodeGraphServer(
            McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null))));

        var result = await GetServerHealthTool.ExecuteAsync(registry, targetPath: solutionPath);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("## Projekt", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveDiagnosticLimit_ReturnsRecoverableInvalidArgument(int maxDiagnostics)
    {
        var solutionPath = SolutionPath(_fixture.RootPath);
        await using var registry = CreateRegistry(solutionPath, CreateReadOnlyServer(RulesPath(solutionPath), _fixture.Snapshot));
        var result = await GetServerHealthTool.ExecuteAsync(
            registry,
            new GetServerHealthOptions(TargetPath: solutionPath, MaxDiagnostics: maxDiagnostics));

        Assert.False(result.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxDiagnostics", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_ReportsLoadStateSolutionAndUptime()
    {
        var solutionPath = SolutionPath(_fixture.RootPath);
        var rulesPath = RulesPath(solutionPath);
        File.WriteAllText(rulesPath, "{ \"Global\": {}, \"Metrics\": {} }");
        await using var registry = CreateRegistry(solutionPath, CreateReadOnlyServer(rulesPath, _fixture.Snapshot));

        var result = await GetServerHealthTool.ExecuteAsync(registry, targetPath: solutionPath);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Version:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Daemon", text, StringComparison.Ordinal);
        Assert.Contains("Loaded", text);
        Assert.Contains(solutionPath, text, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(rulesPath, text, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Uptime", text, StringComparison.Ordinal);
        Assert.Contains("Solution-Refreshes: 0", text);
    }

    [Fact]
    public async Task ExecuteAsync_Loaded_StructuredContentDeserializesToServerHealthPayload()
    {
        var solutionPath = SolutionPath(_fixture.RootPath);
        var rulesPath = RulesPath(solutionPath);
        File.WriteAllText(rulesPath, "{ \"Global\": {}, \"Metrics\": {} }");
        await using var registry = CreateRegistry(solutionPath, CreateReadOnlyServer(rulesPath, _fixture.Snapshot));

        var result = await GetServerHealthTool.ExecuteAsync(registry, targetPath: solutionPath);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.False(payload.TryGetProperty("version", out _));
        Assert.False(payload.TryGetProperty("repository", out _));
        Assert.False(payload.TryGetProperty("daemon", out _));
        var project = payload.GetProperty("project");
        Assert.Equal(solutionPath, project.GetProperty("targetPath").GetString());
        Assert.Equal("Loaded", project.GetProperty("loadState").GetString());
        Assert.Equal(rulesPath, project.GetProperty("configPath").GetString());
        Assert.Equal(0, project.GetProperty("refreshCount").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_NoRulesFile_ReportsNotConfigured()
    {
        var solutionPath = SolutionPath(_fixture.RootPath);
        await using var registry = CreateRegistry(solutionPath, _fixture.CreateReadOnlyServer());

        var result = await GetServerHealthTool.ExecuteAsync(registry, targetPath: solutionPath);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Config: not_configured", text);
    }

    [Fact]
    public async Task ExecuteAsync_GlobalHealthRemainsAggregateWhenDetailFlagIsSet()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        await using var assemblies = new SyntheticAssemblyHealthRegistry(
            new AssemblyAnalysisHealthSnapshot(
                "C:\\fixtures\\global-detail.dll",
                "partial",
                OriginKind: "decompiled",
                ContentHash: "global-hash",
                Diagnostics: ["global-detail-diagnostic", "global-transitive-diagnostic"]));

        var result = await GetServerHealthTool.ExecuteAsync(
            registry,
            assemblies,
            new GetServerHealthOptions(IncludeDiagnostics: true));

        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.False(payload.DiagnosticsIncluded);
        Assert.False(payload.SessionsIncluded);
        Assert.Null(payload.Assemblies);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Diagnosen gesamt: 2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("global-detail-diagnostic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("global-transitive-diagnostic", text, StringComparison.Ordinal);
    }

    private static string SolutionPath(string root) => Path.Combine(root, "SymbolGraphMini.slnx");

    private static string RulesPath(string solutionPath) =>
        Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");

    private static McpCodeGraphServer CreateReadOnlyServer(
        string rulesPath,
        Microsoft.CodeAnalysis.Solution snapshot) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            Config: new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            ResolvedConfigPath: rulesPath,
            ReadOnlySolutionSnapshot: snapshot)));

    private static ProjectRegistry CreateRegistry(string solutionPath, McpCodeGraphServer server)
    {
        var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(server));
        var lease = registry.Lease(solutionPath);
        Assert.True(lease.Succeeded);
        lease.Lease!.Dispose();
        return registry;
    }

    private sealed class SyntheticAssemblyHealthRegistry(AssemblyAnalysisHealthSnapshot snapshot) : IAssemblyAnalysisRegistry
    {
        public int ResidentCount => 1;

        public Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync() =>
            Task.FromResult<IReadOnlyList<AssemblyAnalysisHealthSnapshot>>([snapshot]);

        public Task<AssemblyAnalysisLeaseResult> LeaseAsync(
            string assemblyPath,
            System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
