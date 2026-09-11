#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests.Mcp.Projects;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using static AiNetLinter.TestKit.McpTestResultText;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Wiring;

/// <summary>
/// Vertragstests fuer Target-Guards, Projekt-Lifecycle, Health und Ressourcen.
/// </summary>
[Trait("Category", "Unit")]
// @covers GetServerHealthTool
// @covers ServerMaintenanceToolRegistrations
public sealed class WiringProjectContractTests
{
    [Fact]
    public async Task AnalysisToolCall_MissingTarget_ReturnsRequiredGuardWithoutLease()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var missing = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(null),
            new AnalysisToolDispatch(ProjectCall: _ => throw new InvalidOperationException("darf nicht erreicht werden")));
        Assert.NotEqual(true, missing.IsError);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(missing), StringComparison.Ordinal);
        var blank = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest("   "),
            new AnalysisToolDispatch(ProjectCall: _ => throw new InvalidOperationException("darf nicht erreicht werden")));
        Assert.NotEqual(true, blank.IsError);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(blank), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalysisToolCall_RelativeTargetPath_ReturnsInvalidGuard()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var result = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest("relativ/projekt"),
            new AnalysisToolDispatch(ProjectCall: _ => throw new InvalidOperationException("darf nicht erreicht werden")));
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("[ERROR]: INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalysisToolRoute_AssemblyRegistrationDelegatesToSharedTargetDispatcher()
    {
        using var tempDir = TestTempDirectory.Create("wiring-assembly-route-");
        var assemblyPath = Path.Combine(tempDir.DirectoryPath, "Probe.dll");
        File.WriteAllBytes(assemblyPath, [0]);
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var invoked = false;
        var result = await AnalysisToolCall.ExecuteRouted(
            request =>
            {
                invoked = request.Dispatch.AssemblySessionCall is not null;
                return Task.FromResult(McpToolResults.Text("assembly-route"));
            },
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(AssemblySessionCall: _ => Task.FromResult(McpToolResults.Text("unreachable")))));

        Assert.True(invoked);
        Assert.Equal("assembly-route", TextOf(result));
    }

    [Fact]
    public void ServerInstructions_TextStaysWithinBudgetAndCarriesContract()
    {
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(ServerInstructions.Text);
        Assert.True(byteCount <= ServerInstructions.MaxUtf8Bytes, $"Instructions-Budget gerissen: {byteCount} > {ServerInstructions.MaxUtf8Bytes}");
        Assert.Contains("targetPath", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://agent-guide", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("get_server_health", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains(".dll/.exe", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("structuredContent.navigation", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("tools/list", ServerInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TestKitProjectRoot_UsesAdjacentMcpRulesOnly()
    {
        using var tempDir = TestTempDirectory.Create("wiring-project-root-");

        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");

        Assert.True(File.Exists(Path.Combine(root, "ainetlinter-rules.json")));
    }

    [Fact]
    public void Registry_ReadableButInvalidRules_FailsDeterministicallyWithRulesInvalid()
    {
        using var tempDir = TestTempDirectory.Create("wiring-rules-invalid-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        File.WriteAllText(RulesPath(solutionPath), "{ this is not valid json ");
        var loadResult = ProjectDefinitionLoader.LoadSolutionTarget(solutionPath);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        var creation = ProjectInstanceFactory.TryCreate(
            loadResult.Definition!,
            _ => throw new InvalidOperationException("Bei ungueltiger Regeldatei darf keine Options-Materialisierung laufen."));
        Assert.False(creation.Succeeded);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, creation.ErrorCode);
        Assert.Contains(RulesPath(solutionPath), creation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_ValidRules_MaterializesOptionsFromDefinitionRulesPath()
    {
        using var tempDir = TestTempDirectory.Create("wiring-rules-valid-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        File.WriteAllText(RulesPath(solutionPath), "{ \"Global\": {}, \"Metrics\": { \"MaxLineCount\": 42 } }");
        var definition = ProjectDefinitionLoader.LoadSolutionTarget(solutionPath).Definition!;
        McpCodeGraphServerOptions? captured = null;
        var creation = ProjectInstanceFactory.TryCreate(definition, options =>
        {
            captured = options;
            return ProjectInstanceCreation.Failed("TEST_CAPTURE", "Test erzeugt keine Instanz.");
        });
        Assert.Equal("TEST_CAPTURE", creation.ErrorCode);
        Assert.NotNull(captured);
        Assert.Equal(definition.RulesPath, captured!.ResolvedConfigPath);
        Assert.Equal(42, captured.MaxLineCount);
    }

    [Fact]
    public async Task Lease_FactoryFailure_YieldsRulesInvalidWithoutResidentEntry()
    {
        using var tempDir = TestTempDirectory.Create("wiring-factory-fail-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        File.WriteAllText(RulesPath(solutionPath), "{ broken");
        var created = 0;
        await using var registry = ProjectRegistryFixture.Create(definition =>
            ProjectInstanceFactory.TryCreate(definition, _ =>
            {
                Interlocked.Increment(ref created);
                throw new InvalidOperationException("Fabrik darf bei ungueltigen Regeln nicht erreichen.");
            }));
        var lease = registry.Lease(solutionPath);
        Assert.False(lease.Succeeded);
        Assert.Null(lease.Lease);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, lease.ErrorCode);
        Assert.Equal(0, Volatile.Read(ref created));
        Assert.Null(registry.FindSnapshot(solutionPath));
    }

    [Fact]
    public async Task Lease_StaysOpenForEntireToolCall_EvictionOnlyAfterCompletion()
    {
        using var tempDir = TestTempDirectory.Create("wiring-lease-lifetime-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        var clock = new FakeClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        var callTask = ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            new AnalysisToolDispatch(ProjectCall: async _ =>
            {
                clock.AdvanceMinutes(60);
                await registry.RunEvictionTickAsync();
                Assert.NotNull(registry.FindSnapshot(solutionPath));
                await Task.Delay(50);
                return McpToolResults.Text("ok");
            }));
        var result = await callTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.StartsWith("ok", TextOf(result), StringComparison.Ordinal);
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.Null(registry.FindSnapshot(solutionPath));
    }

    [Fact]
    public async Task ColdLoadFault_AnswersLoadingThenProjectLoadFailed()
    {
        using var tempDir = TestTempDirectory.Create("wiring-cold-fault-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        var console = new RecordingLintConsole();
        var faultingServer = OverviewTestServers.FaultingLoadServer(console);
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(faultingServer));
        var first = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            new AnalysisToolDispatch(ProjectCall: _ => Task.FromResult(McpToolResults.Text("sollte nie erreicht werden"))));
        var failedText = TextOf(first).Contains(ProjectErrorCodes.ProjectLoadFailed, StringComparison.Ordinal)
            ? TextOf(first)
            : null;
        if (failedText is null)
        {
            await TestWaiter.WaitForConditionAsync(
                () => faultingServer.LoadState == ServerLoadState.LoadFailed,
                TimeSpan.FromSeconds(15));
            var failed = await ExecuteProjectAsync(
                registry,
                new AnalysisTargetRequest(solutionPath),
                new AnalysisToolDispatch(ProjectCall: _ => Task.FromResult(McpToolResults.Text("sollte nie erreicht werden"))));
            failedText = TextOf(failed);
        }
        Assert.NotNull(failedText);
        Assert.Contains("[ERROR]: PROJECT_LOAD_FAILED", failedText, StringComparison.Ordinal);
        Assert.Contains(solutionPath, failedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatisch neu", failedText, StringComparison.Ordinal);
        Assert.Contains("Simulierter Kalt-Load-Fehler", failedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DegradedRefresh_LastGoodStateResident_WarnHeaderUntilHealed()
    {
        using var tempDir = TestTempDirectory.Create("wiring-degraded-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        var console = new RecordingLintConsole();
        var attempt = 0;
        var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = CreateCatalog(),
            Console = console,
            Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            LoadFunc = _ =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                {
                    throw new InvalidOperationException("Simulierter Refresh-Fehler");
                }
                return Task.FromResult<SourceFileCatalog?>(CreateCatalog());
            },
        });
        await TestWaiter.WaitForConditionAsync(() => server.LoadState == ServerLoadState.Loaded, TimeSpan.FromSeconds(15));
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(server));
        var healthy = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            new AnalysisToolDispatch(ProjectCall: _ => Task.FromResult(McpToolResults.Text("kernantwort"))));
        Assert.False(TextOf(healthy).StartsWith("[WARN]", StringComparison.Ordinal));
        Assert.False(await server.ReloadSolutionAsync(CancellationToken.None));
        Assert.True(server.HasDegradedAnswerState);
        Assert.NotNull(server.LastGoodStateUtc);
        Assert.Contains("Simulierter Refresh-Fehler", server.LastLoadError, StringComparison.Ordinal);
        var degraded = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            new AnalysisToolDispatch(ProjectCall: _ => Task.FromResult(McpToolResults.Text("kernantwort", new { value = "payload" }))));
        var degradedText = TextOf(degraded);
        Assert.StartsWith("[WARN]", degradedText, StringComparison.Ordinal);
        Assert.Contains("letzten guten Solution-Stand", degradedText, StringComparison.Ordinal);
        Assert.Contains("kernantwort", degradedText, StringComparison.Ordinal);
        Assert.NotNull(degraded.StructuredContent);
        Assert.Equal("payload", degraded.StructuredContent!.Value.GetProperty("value").GetString());
        Assert.True(degraded.StructuredContent.Value.GetProperty("degraded").GetBoolean());
        Assert.Equal("stale", degraded.StructuredContent.Value.GetProperty("freshness").GetString());
        Assert.Equal("refresh-failed", degraded.StructuredContent.Value.GetProperty("degradedReason").GetString());
        Assert.True(await server.ReloadSolutionAsync(CancellationToken.None));
        Assert.False(server.HasDegradedAnswerState);
        var healed = await ExecuteProjectAsync(
            registry,
            new AnalysisTargetRequest(solutionPath),
            new AnalysisToolDispatch(ProjectCall: _ => Task.FromResult(McpToolResults.Text("kernantwort"))));
        Assert.StartsWith("kernantwort", TextOf(healed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_AggregatesAllKeys_AndFiltersSingleKeyWithContracts()
    {
        using var tempDir = TestTempDirectory.Create("wiring-health-");
        var solutionPathA = CreateSolutionPath(tempDir, "alpha");
        var solutionPathB = CreateSolutionPath(tempDir, "beta");
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry();
        OpenAndCloseLease(registry, solutionPathA);
        OpenAndCloseLease(registry, solutionPathB);
        var all = await GetServerHealthTool.ExecuteAsync(registry);
        var allText = TextOf(all);
        Assert.Contains("## Projekte (0)", allText, StringComparison.Ordinal);
        Assert.DoesNotContain(solutionPathA, allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(solutionPathB, allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sessions gesamt:", allText, StringComparison.Ordinal);
        Assert.Contains("Statusverteilung:", allText, StringComparison.Ordinal);
        Assert.Contains("Diagnosen gesamt:", allText, StringComparison.Ordinal);
        var filtered = await GetServerHealthTool.ExecuteAsync(registry, targetPath: solutionPathB);
        var filteredText = TextOf(filtered);
        Assert.Contains(solutionPathB, filteredText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(solutionPathA, filteredText, StringComparison.OrdinalIgnoreCase);
        var unknown = tempDir.CreateFile("unbekannt.slnx", string.Empty);
        var freshlyLoaded = await GetServerHealthTool.ExecuteAsync(registry, targetPath: unknown);
        Assert.NotEqual(true, freshlyLoaded.IsError);
        var freshlyLoadedText = TextOf(freshlyLoaded);
        Assert.Contains(unknown, freshlyLoadedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Projekte (1)", freshlyLoadedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ProjectTarget_UsesDaemonRegistrySnapshotWhenRuntimeContextIsPresent()
    {
        using var tempDir = TestTempDirectory.Create("wiring-health-daemon-route-");
        var solutionPath = CreateSolutionPath(tempDir, "daemon-project");
        await using var daemonRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        OpenAndCloseLease(daemonRegistry, solutionPath);
        await using var localRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        var runtimeContext = new DaemonRuntimeContext(
            17,
            () => new DaemonRuntimeSnapshot(1, 1234, TimeSpan.FromSeconds(3), [solutionPath], "test"),
            daemonRegistry.FindSnapshot);

        var result = await GetServerHealthTool.ExecuteAsync(
            localRegistry,
            new GetServerHealthOptions(TargetPath: solutionPath, RuntimeContext: runtimeContext));

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("## Projekte (1)", text, StringComparison.Ordinal);
        Assert.Contains(solutionPath, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_ProjectTarget_LoadsOnDemandViaDaemonLeaseProviderWhenSnapshotNotPresent()
    {
        using var tempDir = TestTempDirectory.Create("wiring-health-daemon-ondemand-");
        var solutionPath = CreateSolutionPath(tempDir, "daemon-ondemand");
        await using var daemonRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        await using var localRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        var adapter = new DaemonRegistryAdapter(daemonRegistry);
        var runtimeContext = new DaemonRuntimeContext(
            17,
            () => new DaemonRuntimeSnapshot(1, 1234, TimeSpan.FromSeconds(3), [], "test"),
            adapter.FindSnapshot,
            adapter.Lease);

        var result = await GetServerHealthTool.ExecuteAsync(
            localRegistry,
            new GetServerHealthOptions(TargetPath: solutionPath, RuntimeContext: runtimeContext));

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("## Projekte (1)", text, StringComparison.Ordinal);
        Assert.Contains(solutionPath, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Health_ProjectTarget_DaemonRouteRejectsNonPositiveDiagnosticLimit(int maxDiagnostics)
    {
        using var tempDir = TestTempDirectory.Create("wiring-health-daemon-validation-");
        var solutionPath = CreateSolutionPath(tempDir, "daemon-project");
        await using var daemonRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        OpenAndCloseLease(daemonRegistry, solutionPath);
        await using var localRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        var runtimeContext = new DaemonRuntimeContext(
            18,
            () => new DaemonRuntimeSnapshot(1, 1234, TimeSpan.FromSeconds(3), [solutionPath], "test"),
            daemonRegistry.FindSnapshot);

        var result = await GetServerHealthTool.ExecuteAsync(
            localRegistry,
            new GetServerHealthOptions(
                TargetPath: solutionPath,
                RuntimeContext: runtimeContext,
                IncludeDiagnostics: true,
                MaxDiagnostics: maxDiagnostics));

        Assert.False(result.IsError);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal("$.maxDiagnostics", result.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task Overview_TemplateResolvesKeyAndCarriesSameGuards()
    {
        using var tempDir = TestTempDirectory.Create("wiring-overview-");
        var solutionPath = CreateSolutionPath(tempDir, "proj");
        var pendingServer = OverviewTestServers.PendingLoadServer();
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(pendingServer));
        var leaseResult = registry.Lease(solutionPath);
        Assert.True(leaseResult.Succeeded);
        leaseResult.Lease!.Dispose();
        var read = OverviewResourceRegistration.BuildTemplatedResult(registry, solutionPath);
        var textContents = Assert.IsType<TextResourceContents>(Assert.Single(read.Contents));
        Assert.Contains("# AiNetLinter MCP-Server", textContents.Text, StringComparison.Ordinal);
        Assert.Contains(solutionPath, textContents.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wird noch geladen", textContents.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://overview?targetPath=", textContents.Uri, StringComparison.Ordinal);
        Assert.NotEqual(solutionPath, textContents.Uri, StringComparer.Ordinal);
        var equivalent = OverviewResourceRegistration.BuildTemplatedResult(registry, solutionPath.Replace('\\', '/'));
        Assert.Contains(solutionPath, Assert.IsType<TextResourceContents>(Assert.Single(equivalent.Contents)).Text, StringComparison.OrdinalIgnoreCase);
        var exMissing = Assert.Throws<McpException>(() => OverviewResourceRegistration.BuildTemplatedResult(registry, ""));
        Assert.Contains("INVALID_ARGUMENT", exMissing.Message, StringComparison.Ordinal);
        var exRelative = Assert.Throws<McpException>(() => OverviewResourceRegistration.BuildTemplatedResult(registry, "relative/path"));
        Assert.Contains("INVALID_ARGUMENT", exRelative.Message, StringComparison.Ordinal);
        var unknown = tempDir.CreateFile("nirgends.slnx", string.Empty);
        var unknownRead = OverviewResourceRegistration.BuildTemplatedResult(registry, unknown);
        Assert.Contains(
            "wird noch geladen",
            Assert.IsType<TextResourceContents>(Assert.Single(unknownRead.Contents)).Text,
            StringComparison.Ordinal);
    }

    private static void OpenAndCloseLease(ProjectRegistry registry, string solutionPath)
    {
        var leaseResult = registry.Lease(solutionPath);
        Assert.True(leaseResult.Succeeded);
        leaseResult.Lease!.Dispose();
    }

    private static Task<CallToolResult> ExecuteProjectAsync(
        ProjectRegistry registry,
        AnalysisTargetRequest request,
        AnalysisToolDispatch dispatch) =>
        ProjectAnalysisDispatcher.ExecuteAsync(registry, request, dispatch.ProjectCall!);

    private static SourceFileCatalog CreateCatalog() =>
        new(WiringScenario.Solution, hasLoadingErrors: false);

    private static string CreateSolutionPath(TestTempDirectory tempDir, string name)
    {
        var root = ProjectRegistryFixture.CreateProjectRoot(
            tempDir,
            name,
            rulesRelative: "ainetlinter-rules.json");
        return Path.Combine(root, "app.slnx");
    }

    private static string RulesPath(string solutionPath) =>
        Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");

    private static class WiringScenario
    {
        internal static readonly Microsoft.CodeAnalysis.Solution Solution =
            SymbolGraphMiniSolutionSpec.Create().Solution;
    }
}
