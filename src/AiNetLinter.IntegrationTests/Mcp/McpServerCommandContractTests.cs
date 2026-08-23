#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Configuration;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpServerCommandContractTests
{
    private readonly ReadOnlyMcpHostFixture fixture;

    public McpServerCommandContractTests(ReadOnlyMcpHostFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task TryLoadSolutionAsync_BrokenSlnx_LogsWarningAndPropagatesOriginalException()
    {
        var path = CreateTempDir();
        try
        {
            var slnx = Path.Combine(path, "Broken.slnx");
            File.WriteAllText(slnx, "<this-is-not-a-valid-slnx-document>");
            var console = new RecordingLintConsole();
            var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await McpServerCommand.TryLoadSolutionAsync(slnx, CancellationToken.None, console));

            var warning = Assert.Single(console.ErrorLines, line => line.Contains("[WARN]", StringComparison.Ordinal));
            Assert.Contains(exception.Message, warning, StringComparison.Ordinal);
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract()
    {
        await using var scenario = new BrokenColdLoadHarness();
        var initial = scenario.Registry.Lease(scenario.Root);
        Assert.True(initial.Succeeded);
        await scenario.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        initial.Lease!.Dispose();
        scenario.ArmReleaseHook();

        var loading = await ProjectToolCall.ExecuteAsync(
            scenario.Registry,
            scenario.Root,
            _ => Task.FromResult(McpToolResults.Text("unerwartet geladen")));
        Assert.Contains("laedt die Solution noch", Assert.IsType<TextContentBlock>(Assert.Single(loading.Content)).Text, StringComparison.Ordinal);
        var originalException = await scenario.LoadFailure.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotEmpty(originalException.Message);

        var failed = await ProjectToolCall.ExecuteAsync(
            scenario.Registry,
            scenario.Root,
            _ => Task.FromResult(McpToolResults.Text("unerwartet geladen")));
        var failedText = Assert.IsType<TextContentBlock>(Assert.Single(failed.Content)).Text;
        var warning = Assert.Single(scenario.Console.ErrorLines, line => line.Contains("[WARN]", StringComparison.Ordinal));
        Assert.Contains("[WARN]", warning, StringComparison.Ordinal);
        Assert.Contains(originalException.Message, warning, StringComparison.Ordinal);
        Assert.Contains("PROJECT_LOAD_FAILED", failedText, StringComparison.Ordinal);
        Assert.Contains(originalException.Message, failedText, StringComparison.Ordinal);
        Assert.Contains(scenario.SolutionPath, failedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatisch neu", failedText, StringComparison.Ordinal);

        var failedServer = scenario.Server;
        var retry = scenario.Registry.Lease(scenario.Root);
        using var retryLease = retry.Lease;
        Assert.Equal(2, scenario.FactoryCalls);
        Assert.NotSame(failedServer, retryLease!.Server);
    }

    [Fact]
    public async Task ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault()
    {
        var path = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(path, "Only.slnx"), "");
            var args = new LinterArgs { ConfigPath = null, TargetPath = path, Verbose = false };

            Assert.NotNull(McpServerCommand.ResolveConfig(args, null));
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task RunAsync_ValidFixture_ServerRespondsWithAllTools()
    {
        var tools = await (await fixture.GetHostAsync()).ListToolsAsync();
        Assert.Equal(26, tools.Count);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetHotspotsReturnsAllGreenForSmallFixture() =>
        await AssertTextAsync("get_hotspots", new Dictionary<string, object?>(), "im gruenen Bereich");

    [Fact]
    public async Task RunAsync_ValidFixture_GetIndexScopeReturnsFileTypeBreakdown() =>
        await AssertTextAsync("get_index_scope", new Dictionary<string, object?>(), ".cs:");

    [Fact]
    public async Task RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation() =>
        await AssertTextAsync("get_violations", new Dictionary<string, object?>(), "ViolationTrigger");

    [Fact]
    public async Task RunAsync_ValidFixture_SearchPatternReturnsExpectedHit() =>
        await AssertTextAsync("search_pattern", new Dictionary<string, object?> { ["pattern"] = "Greeter" }, "Greeter.cs");

    [Fact]
    public async Task RunAsync_ValidFixture_SearchPatternStructuredArgumentsBind()
    {
        var result = await (await fixture.GetHostAsync()).CallToolAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "Greeter",
                ["maxFiles"] = 1,
                ["contextLines"] = 1,
                ["maxResponseBytes"] = 4096,
                ["scope"] = "src",
                ["includePatterns"] = new[] { "**/*.cs" },
                ["enrichCSharp"] = true,
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        Assert.Equal(
            System.Text.Json.JsonValueKind.Object,
            result.StructuredContent!.Value.ValueKind);
        var matches = result.StructuredContent.Value.GetProperty("matches").EnumerateArray().ToArray();
        Assert.NotEmpty(matches);
        Assert.Contains(matches, match => match.TryGetProperty("semantic", out var semantic)
            && semantic.ValueKind == System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task SearchPatternRegistration_AdvertisesOptInEnrichment()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tool = McpServerOptionsFactory.Create(registry).ToolCollection!
            .Single(candidate => candidate.ProtocolTool.Name == "search_pattern");

        Assert.Contains("enrichCSharp", tool.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.Contains("enrichCSharp=true", tool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("ambiguous", tool.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("unavailable", tool.ProtocolTool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_FindSymbolReturnsMatch() =>
        await AssertTextAsync("find_symbol", new Dictionary<string, object?> { ["namePattern"] = "Greeter" }, "Greeter");

    [Fact]
    public async Task RunAsync_ValidFixture_FindReferencesReturnsCallSite() =>
        await AssertTextAsync("find_references", new Dictionary<string, object?> { ["symbolIdentifier"] = "Greeter.Greet" }, "Caller.cs");

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        workspace.CommitCalculatorAddBodyChange();
        await using var host = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));
        var text = await host.CallToolGetTextAsync("get_impact", new Dictionary<string, object?> { ["gitRef"] = "HEAD~1" });
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite()
    {
        var workspace = new GitImpactMiniFixtureWorkspace();
        workspace.ChangeCalculatorAddBodyWithoutCommitting();
        await using var host = await McpProcessHost.StartAsync(workspace, TimeSpan.FromSeconds(60));
        var text = await host.CallToolGetTextAsync("get_impact", new Dictionary<string, object?>());
        Assert.Contains("CalculatorCaller.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ValidFixture_GetFileSkeletonReturnsGreeterSignature() =>
        await AssertTextAsync("get_file_skeleton", new Dictionary<string, object?> { ["filePath"] = "src/SymbolGraphMini/Greeter.cs" }, "Greet");

    [Fact]
    public async Task RunAsync_ValidFixture_GetTypeHierarchyReturnsBaseGreetingHierarchy() =>
        await AssertTextAsync("get_type_hierarchy", new Dictionary<string, object?> { ["symbolIdentifier"] = "BaseGreeting" }, "IGreeting");

    private sealed class BrokenColdLoadHarness : IAsyncDisposable
    {
        private readonly TestTempDirectory tempDir;
        private readonly TaskCompletionSource releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim loadFaultPublished = new(false);
        private int factoryCalls;
        private int releaseHookArmed;
        private int faultReleaseHookCalls;

        internal BrokenColdLoadHarness()
        {
            tempDir = TestTempDirectory.Create("mcp-production-cold-load-");
            Root = ProjectRegistryFixture.CreateProjectRoot(tempDir, "broken");
            SolutionPath = Path.Combine(Root, "app.slnx");
            File.WriteAllText(SolutionPath, "<this-is-not-a-valid-slnx-document>");
            LoadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            LoadFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Console = new RecordingLintConsole();
            Registry = new ProjectRegistry(new ProjectRegistryOptions(CreateInstance, TimeProvider.System)
            {
                BeforeLeaseRelease = BeforeLeaseRelease,
            });
        }

        internal string Root { get; }

        internal string SolutionPath { get; }

        internal RecordingLintConsole Console { get; }

        internal TaskCompletionSource LoadStarted { get; }

        internal TaskCompletionSource<Exception> LoadFailure { get; }

        internal ProjectRegistry Registry { get; }

        internal McpCodeGraphServer? Server { get; private set; }

        internal int FactoryCalls => Volatile.Read(ref factoryCalls);

        internal void ArmReleaseHook() => Volatile.Write(ref releaseHookArmed, 1);

        public async ValueTask DisposeAsync()
        {
            await Registry.DisposeAsync();
            loadFaultPublished.Dispose();
            tempDir.Dispose();
        }

        private ProjectInstanceCreation CreateInstance(ProjectDefinition definition)
        {
            Interlocked.Increment(ref factoryCalls);
            Server = new McpCodeGraphServer(new McpCodeGraphServerOptions
            {
                Catalog = null,
                Console = Console,
                Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
                UsedDefaultConfig = false,
                LoadFunc = LoadSolutionAsync,
            });
            return ProjectInstanceCreation.Resident(Server);
        }

        private async Task<SourceFileCatalog?> LoadSolutionAsync(CancellationToken cancellationToken)
        {
            LoadStarted.TrySetResult();
            await releaseLoad.Task.WaitAsync(cancellationToken);
            try
            {
                return await McpServerCommand.TryLoadSolutionAsync(SolutionPath, cancellationToken, Console);
            }
            catch (Exception exception)
            {
                LoadFailure.TrySetResult(exception);
                loadFaultPublished.Set();
                throw;
            }
        }

        private void BeforeLeaseRelease()
        {
            if (Volatile.Read(ref releaseHookArmed) == 1
                && Interlocked.Exchange(ref faultReleaseHookCalls, 1) == 0)
            {
                releaseLoad.TrySetResult();
                Assert.True(loadFaultPublished.Wait(TimeSpan.FromSeconds(10)));
            }
        }
    }

    private async Task AssertTextAsync(string tool, IReadOnlyDictionary<string, object?> arguments, string expected)
    {
        var result = await (await fixture.GetHostAsync()).CallToolAsync(tool, arguments);
        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(expected, text, StringComparison.Ordinal);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(TestTempDirectory.RootTempDirectory, $"ainetlinter-mcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
