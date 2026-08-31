#nullable enable

using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
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

        var result = await GetServerHealthTool.ExecuteAsync(registry);

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

        var result = await GetServerHealthTool.ExecuteAsync(registry);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Version));
        Assert.Equal("Loaded", Assert.Single(payload.Projects).LoadState);
        Assert.Equal(0, Assert.Single(payload.Projects).RefreshCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsedDefaultConfig_MentionsDefaultRules()
    {
        await using var registry = CreateRegistry(_fixture.RootPath, _fixture.CreateReadOnlyServer(usedDefaultConfig: true));

        var result = await GetServerHealthTool.ExecuteAsync(registry);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Default-Regeln", text);
    }

    [Fact]
    public void Build_DefaultHealthIsCompact_AndDetailDiagnosticsStayBounded()
    {
        var entry = new AssemblyHealthEntry(
            "C:\\fixtures\\health.dll",
            "partial",
            "decompiled",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            ["health-root-0", "health-root-1"],
            TransitiveDiagnostics: ["health-transitive-0", "health-transitive-1"]);

        var compact = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions());
        var compactPayload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            compact.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        var compactAssembly = Assert.Single(compactPayload.Assemblies!);
        Assert.False(compactPayload.DiagnosticsIncluded);
        Assert.Null(compactAssembly.Diagnostics);
        Assert.Equal(4, compactAssembly.DiagnosticsSummary!.TotalCount);
        Assert.Empty(compactAssembly.DiagnosticsSummary.Samples);
        Assert.Equal(2, compactAssembly.DiagnosticsSummary.Root.TotalCount);
        Assert.Equal(2, compactAssembly.DiagnosticsSummary.Transitive.TotalCount);
        Assert.Empty(compactAssembly.DiagnosticsSummary.Root.Samples);
        Assert.Empty(compactAssembly.DiagnosticsSummary.Transitive.Samples);
        Assert.Equal("partial", compactAssembly.Completeness);
        Assert.DoesNotContain("health-root-0", Assert.IsType<TextContentBlock>(Assert.Single(compact.Content)).Text, StringComparison.Ordinal);
        Assert.DoesNotContain("health-transitive-0", Assert.IsType<TextContentBlock>(Assert.Single(compact.Content)).Text, StringComparison.Ordinal);

        var detailed = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, MaxDiagnostics: 2));
        var detailedPayload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            detailed.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        var detailedAssembly = Assert.Single(detailedPayload.Assemblies!);
        Assert.True(detailedPayload.DiagnosticsIncluded);
        Assert.Equal(["health-root-0", "health-transitive-0"], detailedAssembly.Diagnostics);
        Assert.True(detailedAssembly.DiagnosticsSummary!.Truncated);
        Assert.Equal(4, detailedAssembly.DiagnosticsSummary.TotalCount);
        Assert.Equal(2, detailedAssembly.DiagnosticsSummary.ShownCount);
        Assert.Contains("health-root-0", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("health-transitive-0", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
        Assert.DoesNotContain("health-root-1", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
        Assert.DoesNotContain("health-transitive-1", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
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
}
