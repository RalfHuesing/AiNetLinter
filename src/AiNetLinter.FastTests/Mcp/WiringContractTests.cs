#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests.Mcp.Projects;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;
namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Vertragstests des Registry-Wirings: eingefrorener 29er-Toolbestand mit projectRoot-Pflicht
/// (einzige Ausnahme: get_server_health-Filter optional),
/// Defense-in-Depth-Guards am ProjectToolCall, Lease-Lifetime ueber den gesamten Tool-Call,
/// RULES_INVALID statt Default-Fallback, Health-Aggregation je Key, Overview-Template-Aufloesung,
/// zweistufiger Zustandsvertrag (PROJECT_LOAD_FAILED, [WARN]-Kopf, Heilung) und das
/// ServerInstructions-Budget.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WiringContractTests
{
    [Fact]
    public void ToolCollection_FreezesInventoryAndProjectRootContract()
    {
        var options = McpServerOptionsFactory.Create(ProjectRegistryFixture.CreateInspectionRegistry());
        var tools = options.ToolCollection!.ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool);
        Assert.Equal(29, tools.Count);
        foreach (var tool in tools.Values)
        {
            var required = GetRequiredProperties(tool.InputSchema);
            if (tool.Name is "get_server_health" or "report_observability_feedback" or "inspect_assembly" or "find_assembly_extensions")
            {
                Assert.DoesNotContain("projectRoot", required);
                Assert.Contains("\"projectRoot\"", tool.InputSchema.ToString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("projectRoot", required);
            }
        }
    }

    [Fact]
    public async Task ToolCollection_ClassifiesEveryRegisteredToolWithExplicitAnnotations()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerOptionsFactory.BuildToolCollection(registry)
            .ToDictionary(tool => tool.ProtocolTool.Name, StringComparer.Ordinal);
        Assert.Equal(
            ExpectedToolAnnotations.Keys.OrderBy(name => name, StringComparer.Ordinal),
            tools.Keys.OrderBy(name => name, StringComparer.Ordinal));
        foreach (var (name, expected) in ExpectedToolAnnotations)
        {
            var annotations = tools[name].ProtocolTool.Annotations;
            Assert.NotNull(annotations);
            Assert.Equal(expected.ReadOnly, annotations!.ReadOnlyHint);
            Assert.Equal(expected.Destructive, annotations.DestructiveHint);
            Assert.Equal(expected.Idempotent, annotations.IdempotentHint);
            Assert.Equal(expected.OpenWorld, annotations.OpenWorldHint);
        }
    }

    private static readonly IReadOnlyDictionary<string, ToolAnnotationExpectation> ExpectedToolAnnotations =
        new Dictionary<string, ToolAnnotationExpectation>(StringComparer.Ordinal)
        {
            ["dependency_graph"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_assembly_extensions"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_dead_code"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_duplicates"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_magic_values"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_references"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["find_symbol"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_call_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_class_structure"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_feature_context"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_file_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_file_skeleton"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_hotspots"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_index_scope"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_namespace_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_server_health"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_symbol_body"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_test_context"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_type_hierarchy"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_impact"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["get_violations"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["metrics_lookup"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["metrics_tree"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["pattern_detect"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["reload_config"] = new(false, false, true, false),
            ["report_observability_feedback"] = new(false, false, false, false),
            ["safeguard"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["search_pattern"] = ToolAnnotationExpectation.ReadOnlyProfile,
            ["inspect_assembly"] = ToolAnnotationExpectation.ReadOnlyProfile,
        };

    private readonly record struct ToolAnnotationExpectation(
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld)
    {
        internal static ToolAnnotationExpectation ReadOnlyProfile => new(true, false, true, false);
    }

    [Fact]
    public async Task ProjectToolCall_MissingProjectRoot_ReturnsRequiredGuardWithoutLease()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var missing = await ProjectToolCall.ExecuteAsync(registry, null, _ => throw new InvalidOperationException("darf nicht erreicht werden"));
        Assert.True(missing.IsError);
        Assert.Contains("[ERROR]: PROJECT_ROOT_REQUIRED", TextOf(missing), StringComparison.Ordinal);
        var blank = await ProjectToolCall.ExecuteAsync(registry, "   ", _ => throw new InvalidOperationException("darf nicht erreicht werden"));
        Assert.True(blank.IsError);
        Assert.Contains("[ERROR]: PROJECT_ROOT_REQUIRED", TextOf(blank), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectToolCall_RelativeProjectRoot_ReturnsInvalidGuard()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var result = await ProjectToolCall.ExecuteAsync(
            registry,
            "relativ/projekt",
            _ => throw new InvalidOperationException("darf nicht erreicht werden"));
        Assert.True(result.IsError);
        Assert.Contains("[ERROR]: PROJECT_ROOT_INVALID", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilesystemDispatch_MissingOrRelativeProjectRoot_ReturnsExistingGuardWithoutLease()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        foreach (var (root, code) in new (string? Root, string Code)[]
        {
            (null, ProjectErrorCodes.ProjectRootRequired),
            ("   ", ProjectErrorCodes.ProjectRootRequired),
            ("relativ/projekt", ProjectErrorCodes.ProjectRootInvalid),
        })
        {
            var result = await ProjectToolCall.ExecuteFilesystemAsync(registry, root, ThrowingFilesystemCallback);
            Assert.True(result.IsError);
            Assert.Contains($"[ERROR]: {code}", TextOf(result), StringComparison.Ordinal);
        }
        Assert.Empty(registry.Snapshots());
    }
    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackWhileServerIsLoading()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-loading-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var pendingServer = OverviewTestServers.PendingLoadServer();
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(pendingServer));
        var result = await ProjectToolCall.ExecuteFilesystemAsync(
            registry, root, lease => AssertFilesystemCallback(lease, ServerLoadState.Loading, root));
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("physisch", TextOf(result));
    }

    [Fact]
    public async Task FilesystemDispatch_InvokesCallbackAfterLoadFailure()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-failed-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var console = new RecordingLintConsole();
        var faultingServer = OverviewTestServers.FaultingLoadServer(console);
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(faultingServer));
        await WaitForConditionAsync(() => faultingServer.LoadState == ServerLoadState.LoadFailed, TimeSpan.FromSeconds(15));
        var result = await ProjectToolCall.ExecuteFilesystemAsync(
            registry, root, lease => AssertFilesystemCallback(lease, ServerLoadState.LoadFailed));
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("physisch", TextOf(result));
        Assert.DoesNotContain(ProjectErrorCodes.ProjectLoadFailed, TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilesystemDispatch_HoldsLeaseUntilCallbackCompletes()
    {
        using var tempDir = TestTempDirectory.Create("wiring-filesystem-lease-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var clock = new FakeClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        var result = await ProjectToolCall.ExecuteFilesystemAsync(
            registry, root, _ => HoldFilesystemLeaseAsync(registry, root, clock)).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("ok", TextOf(result));
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.Null(registry.FindSnapshot(root));
    }

    [Fact]
    public void ServerInstructions_TextStaysWithinBudgetAndCarriesContract()
    {
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(ServerInstructions.Text);
        Assert.True(byteCount <= ServerInstructions.MaxUtf8Bytes, $"Instructions-Budget gerissen: {byteCount} > {ServerInstructions.MaxUtf8Bytes}");
        Assert.Contains("projectRoot", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://agent-guide", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("get_server_health", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("ainetlinter.project.json", ServerInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_ReadableButInvalidRules_FailsDeterministicallyWithRulesInvalid()
    {
        using var tempDir = TestTempDirectory.Create("wiring-rules-invalid-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        File.WriteAllText(Path.Combine(root, "rules.json"), "{ this is not valid json ");
        var loadResult = ProjectDefinitionLoader.Load(root);
        Assert.True(loadResult.Succeeded, loadResult.Message);
        var creation = ProjectInstanceFactory.TryCreate(
            loadResult.Definition!,
            _ => throw new InvalidOperationException("Bei ungueltiger Regeldatei darf keine Options-Materialisierung laufen."));
        Assert.False(creation.Succeeded);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, creation.ErrorCode);
        Assert.Contains(root, creation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_ValidRules_MaterializesOptionsFromDefinitionRulesPath()
    {
        using var tempDir = TestTempDirectory.Create("wiring-rules-valid-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        File.WriteAllText(Path.Combine(root, "rules.json"), "{ \"Global\": {}, \"Metrics\": { \"MaxLineCount\": 42 } }");
        var definition = ProjectDefinitionLoader.Load(root).Definition!;
        McpCodeGraphServerOptions? captured = null;
        var creation = ProjectInstanceFactory.TryCreate(definition, options =>
        {
            captured = options;
            return ProjectInstanceCreation.Failed("TEST_CAPTURE", "Test erzeugt keine Instanz.");
        });
        Assert.Equal("TEST_CAPTURE", creation.ErrorCode);
        Assert.NotNull(captured);
        Assert.Equal(definition.RulesPath, captured!.ResolvedConfigPath);
        Assert.False(captured.UsedDefaultConfig);
        Assert.Equal(42, captured.MaxLineCount);
    }

    [Fact]
    public async Task Lease_FactoryFailure_YieldsRulesInvalidWithoutResidentEntry()
    {
        using var tempDir = TestTempDirectory.Create("wiring-factory-fail-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        File.WriteAllText(Path.Combine(root, "rules.json"), "{ broken");
        var created = 0;
        await using var registry = ProjectRegistryFixture.Create(definition =>
            ProjectInstanceFactory.TryCreate(definition, _ =>
            {
                Interlocked.Increment(ref created);
                throw new InvalidOperationException("Fabrik darf bei ungueltigen Regeln nicht erreichen.");
            }));
        var lease = registry.Lease(root);
        Assert.False(lease.Succeeded);
        Assert.Null(lease.Lease);
        Assert.Equal(ProjectErrorCodes.RulesInvalid, lease.ErrorCode);
        Assert.Equal(0, Volatile.Read(ref created));
        Assert.Null(registry.FindSnapshot(root));
    }

    [Fact]
    public async Task Lease_StaysOpenForEntireToolCall_EvictionOnlyAfterCompletion()
    {
        using var tempDir = TestTempDirectory.Create("wiring-lease-lifetime-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var clock = new FakeClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        var callTask = ProjectToolCall.ExecuteAsync(registry, root, async _ =>
        {
            clock.AdvanceMinutes(60);
            await registry.RunEvictionTickAsync();
            // Lease ist waehrend des gesamten Calls offen: Busy-Guard verhindert das Raeumen.
            Assert.NotNull(registry.FindSnapshot(root));
            await Task.Delay(50);
            return McpToolResults.Text("ok");
        });
        var result = await callTask.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("ok", TextOf(result));
        // Erst nach Abschluss des Calls darf der Key raeumbar sein.
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.Null(registry.FindSnapshot(root));
    }

    [Fact]
    public async Task ColdLoadFault_AnswersLoadingThenProjectLoadFailed()
    {
        using var tempDir = TestTempDirectory.Create("wiring-cold-fault-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var console = new RecordingLintConsole();
        var faultingServer = OverviewTestServers.FaultingLoadServer(console);
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(faultingServer));
        string? failedText = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (failedText is null && DateTime.UtcNow < deadline)
        {
            var result = await ProjectToolCall.ExecuteAsync(
                registry,
                root,
                _ => Task.FromResult(McpToolResults.Text("sollte nie erreicht werden")));
            var text = TextOf(result);
            if (text.Contains(ProjectErrorCodes.ProjectLoadFailed, StringComparison.Ordinal))
            {
                failedText = text;
                break;
            }
            await Task.Delay(20);
        }
        Assert.NotNull(failedText);
        Assert.Contains("[ERROR]: PROJECT_LOAD_FAILED", failedText, StringComparison.Ordinal);
        Assert.Contains(root, failedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatisch neu", failedText, StringComparison.Ordinal);
        Assert.Contains("Simulierter Kalt-Load-Fehler", failedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DegradedRefresh_LastGoodStateResident_WarnHeaderUntilHealed()
    {
        using var tempDir = TestTempDirectory.Create("wiring-degraded-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var console = new RecordingLintConsole();
        var attempt = 0;
        var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = CreateCatalog(),
            Console = console,
            Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            UsedDefaultConfig = false,
            LoadFunc = _ =>
            {
                if (Interlocked.Increment(ref attempt) == 1)
                {
                    throw new InvalidOperationException("Simulierter Refresh-Fehler");
                }
                return Task.FromResult<SourceFileCatalog?>(CreateCatalog());
            },
        });
        await WaitForConditionAsync(() => server.LoadState == ServerLoadState.Loaded, TimeSpan.FromSeconds(15));
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(server));
        var healthy = await ProjectToolCall.ExecuteAsync(registry, root, _ => Task.FromResult(McpToolResults.Text("kernantwort")));
        Assert.False(TextOf(healthy).StartsWith("[WARN]", StringComparison.Ordinal));
        Assert.False(await server.ReloadSolutionAsync(CancellationToken.None));
        Assert.True(server.HasDegradedAnswerState);
        Assert.NotNull(server.LastGoodStateUtc);
        Assert.Contains("Simulierter Refresh-Fehler", server.LastLoadError, StringComparison.Ordinal);
        var degraded = await ProjectToolCall.ExecuteAsync(
            registry,
            root,
            _ => Task.FromResult(McpToolResults.Text("kernantwort", new { value = "payload" })));
        var degradedText = TextOf(degraded);
        Assert.StartsWith("[WARN]", degradedText, StringComparison.Ordinal);
        Assert.Contains("letzten guten Solution-Stand", degradedText, StringComparison.Ordinal);
        Assert.Contains("kernantwort", degradedText, StringComparison.Ordinal);
        Assert.NotNull(degraded.StructuredContent);
        Assert.Equal("payload", degraded.StructuredContent!.Value.GetProperty("value").GetString());
        Assert.True(await server.ReloadSolutionAsync(CancellationToken.None));
        Assert.False(server.HasDegradedAnswerState);
        var healed = await ProjectToolCall.ExecuteAsync(registry, root, _ => Task.FromResult(McpToolResults.Text("kernantwort")));
        Assert.StartsWith("kernantwort", TextOf(healed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_AggregatesAllKeys_AndFiltersSingleKeyWithContracts()
    {
        using var tempDir = TestTempDirectory.Create("wiring-health-");
        var rootA = ProjectRegistryFixture.CreateProjectRoot(tempDir, "alpha");
        var rootB = ProjectRegistryFixture.CreateProjectRoot(tempDir, "beta");
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry();
        OpenAndCloseLease(registry, rootA);
        OpenAndCloseLease(registry, rootB);
        var all = await GetServerHealthTool.ExecuteAsync(registry);
        var allText = TextOf(all);
        Assert.Contains("## Projekte (2)", allText, StringComparison.Ordinal);
        Assert.Contains(rootA, allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(rootB, allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Zuletzt genutzt (UTC):", allText, StringComparison.Ordinal);
        Assert.Contains("Uptime:", allText, StringComparison.Ordinal);
        Assert.Contains("Solution-Refreshes seit Start:", allText, StringComparison.Ordinal);
        Assert.Contains("Letzter guter Zustand (UTC):", allText, StringComparison.Ordinal);
        var filtered = await GetServerHealthTool.ExecuteAsync(registry, projectRoot: rootB);
        var filteredText = TextOf(filtered);
        Assert.Contains(rootB, filteredText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rootA, filteredText, StringComparison.OrdinalIgnoreCase);
        var unknown = Path.Combine(tempDir.DirectoryPath, "unbekannt");
        var notInitialized = await GetServerHealthTool.ExecuteAsync(registry, projectRoot: unknown);
        Assert.True(notInitialized.IsError);
        Assert.Contains("[ERROR]: PROJECT_NOT_INITIALIZED", TextOf(notInitialized), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overview_TemplateResolvesKeyAndCarriesSameGuards()
    {
        using var tempDir = TestTempDirectory.Create("wiring-overview-");
        var root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "proj");
        var pendingServer = OverviewTestServers.PendingLoadServer();
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(pendingServer));
        var leaseResult = registry.Lease(root);
        Assert.True(leaseResult.Succeeded);
        leaseResult.Lease!.Dispose();
        var read = OverviewResourceRegistration.BuildTemplatedResult(registry, root);
        var textContents = Assert.IsType<TextResourceContents>(Assert.Single(read.Contents));
        Assert.Contains("# AiNetLinter MCP-Server", textContents.Text, StringComparison.Ordinal);
        Assert.Contains(root, textContents.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wird noch geladen", textContents.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://overview?projectRoot=", textContents.Uri, StringComparison.Ordinal);
        Assert.NotEqual(root, textContents.Uri, StringComparer.Ordinal); // kanonische URI ist URL-kodiert
        // Key-Aequivalenz: abweichende Schreibweise desselben Roots trifft denselben Key.
        var equivalent = OverviewResourceRegistration.BuildTemplatedResult(registry, root.Replace('\\', '/'));
        Assert.Contains(root, Assert.IsType<TextResourceContents>(Assert.Single(equivalent.Contents)).Text, StringComparison.OrdinalIgnoreCase);
        var exMissing = Assert.Throws<McpException>(() => OverviewResourceRegistration.BuildTemplatedResult(registry, ""));
        Assert.Contains("PROJECT_ROOT_REQUIRED", exMissing.Message, StringComparison.Ordinal);
        var exRelative = Assert.Throws<McpException>(() => OverviewResourceRegistration.BuildTemplatedResult(registry, "relative/path"));
        Assert.Contains("PROJECT_ROOT_INVALID", exRelative.Message, StringComparison.Ordinal);
        var unknown = Path.Combine(tempDir.DirectoryPath, "nirgends");
        var exUnknown = Assert.Throws<McpException>(() => OverviewResourceRegistration.BuildTemplatedResult(registry, unknown));
        Assert.Contains("PROJECT_NOT_INITIALIZED", exUnknown.Message, StringComparison.Ordinal);
    }

    private static void OpenAndCloseLease(ProjectRegistry registry, string root)
    {
        var leaseResult = registry.Lease(root);
        Assert.True(leaseResult.Succeeded);
        leaseResult.Lease!.Dispose();
    }

    private static Task<CallToolResult> ThrowingFilesystemCallback(ProjectLease _) =>
        throw new InvalidOperationException("darf nicht erreicht werden");

    private static Task<CallToolResult> AssertFilesystemCallback(
        ProjectLease lease, ServerLoadState expectedState, string? expectedRoot = null)
    {
        Assert.Equal(expectedState, lease.Server.LoadState);
        if (expectedRoot is not null)
        {
            var canonicalRoot = Path.GetFullPath(expectedRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Equal(canonicalRoot, lease.RootPath);
        }
        if (expectedState == ServerLoadState.LoadFailed)
        {
            Assert.False(lease.LoadFailedResponseEmitted);
        }
        return Task.FromResult(McpToolResults.Text("physisch"));
    }

    private static async Task<CallToolResult> HoldFilesystemLeaseAsync(
        ProjectRegistry registry, string root, FakeClock clock)
    {
        clock.AdvanceMinutes(60);
        await registry.RunEvictionTickAsync();
        Assert.NotNull(registry.FindSnapshot(root));
        await Task.Delay(50);
        return McpToolResults.Text("ok");
    }

    private static string[] GetRequiredProperties(JsonElement inputSchema)
    {
        using var document = JsonDocument.Parse(inputSchema.ToString());
        if (!document.RootElement.TryGetProperty("required", out var required)
            || required.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return required.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static string TextOf(CallToolResult result) =>
        result.Content is { Count: > 0 } && result.Content[0] is TextContentBlock text ? text.Text ?? string.Empty : string.Empty;

    private static SourceFileCatalog CreateCatalog() =>
        new(WiringScenario.Solution, hasLoadingErrors: false);

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.True(condition(), "Bedingung wurde nicht innerhalb des Zeitlimits erfuellt.");
    }

    private static class WiringScenario
    {
        internal static readonly Microsoft.CodeAnalysis.Solution Solution =
            SymbolGraphMiniSolutionSpec.Create().Solution;
    }
}
