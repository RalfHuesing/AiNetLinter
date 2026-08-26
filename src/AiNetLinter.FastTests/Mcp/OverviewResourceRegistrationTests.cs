#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Tests fuer die MCP-Resource <c>ainetlinter://overview?projectRoot=...</c>
/// (<see cref="OverviewResourceRegistration"/>): dynamischer Status-Text je Projekt-Key
/// (Solution-Pfad, Config-Quelle, Loading-Zustand) und Guards/Fehlervertraege des Templates.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OverviewResourceRegistrationTests
{
    [Fact]
    public void BuildOverviewText_DefaultRules_MentionsDefaultRules()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, UsedDefaultConfig: true, ResolvedConfigPath: null)));
        using var harness = OverviewSnapshotHarness.Create(state);

        var text = OverviewResourceRegistration.BuildOverviewText(harness.Snapshot);

        Assert.Contains("keine rules.json gefunden", text, StringComparison.Ordinal);
        Assert.Contains("Default-Regeln", text, StringComparison.Ordinal);
        Assert.Contains(harness.RootPath, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Zuletzt genutzt (UTC):", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOverviewText_ExplicitConfig_ShowsResolvedConfigPath()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null, UsedDefaultConfig: false, ResolvedConfigPath: @"C:\Projekt\rules.json")));
        using var harness = OverviewSnapshotHarness.Create(state);

        var text = OverviewResourceRegistration.BuildOverviewText(harness.Snapshot);

        Assert.Contains(@"C:\Projekt\rules.json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Default-Regeln", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOverviewText_ListsStatusAndNextSteps()
    {
        var state = PendingLoadServer();
        using var harness = OverviewSnapshotHarness.Create(state);

        var text = OverviewResourceRegistration.BuildOverviewText(harness.Snapshot);

        Assert.Contains("wird noch geladen", text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://agent-guide", text, StringComparison.Ordinal);
        Assert.Contains("tools/list", text, StringComparison.Ordinal);
        Assert.Contains("get_impact", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTemplatedResult_UnknownKey_ThrowsProjectNotInitialized()
    {
        using var tempDir = TestTempDirectory.Create("overview-template-");
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();

        var unknown = Path.Combine(tempDir.DirectoryPath, "nirgends");
        var exception = Assert.Throws<McpException>(
            () => OverviewResourceRegistration.BuildTemplatedResult(registry, unknown));

        Assert.Contains("PROJECT_NOT_INITIALIZED", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ainetlinter.project.json", exception.Message, StringComparison.Ordinal);
    }

    private static McpCodeGraphServer PendingLoadServer() =>
        new(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => pending.TrySetCanceled(token));
                return pending.Task;
            },
        });
}

/// <summary>Kombiniert einen Server mit einem echten Definitionsdatensatz zu einem Snapshot.</summary>
internal sealed class OverviewSnapshotHarness : IDisposable
{
    private readonly TestTempDirectory tempDir = TestTempDirectory.Create("overview-snap-");
    private bool disposed;

    private OverviewSnapshotHarness(McpCodeGraphServer server)
    {
        Server = server;
    }

    public McpCodeGraphServer Server { get; }

    public string RootPath { get; private set; } = string.Empty;

    public ProjectSnapshot Snapshot { get; private set; } =
        new(string.Empty, new ProjectDefinition(string.Empty, string.Empty), DateTime.UtcNow, null!);

    public static OverviewSnapshotHarness Create(McpCodeGraphServer server)
    {
        var harness = new OverviewSnapshotHarness(server);
        var root = ProjectRegistryFixture.CreateProjectRoot(harness.tempDir, "proj");
        var definition = ProjectDefinitionLoader.Load(root).Definition!;
        harness.RootPath = root;
        harness.Snapshot = new ProjectSnapshot(root, definition, DateTime.UtcNow, server);
        return harness;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Server.Dispose();
        tempDir.Dispose();
    }
}
