#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
internal sealed class FakeClock : TimeProvider
{
    private long utcTicks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

    public override DateTimeOffset GetUtcNow() => new(Volatile.Read(ref utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan delta) => Interlocked.Add(ref utcTicks, delta.Ticks);

    public void AdvanceMinutes(int minutes) => Advance(TimeSpan.FromMinutes(minutes));
}

[Trait("Category", "Unit")]
internal sealed class TrackingServerFactory
{
    private int instancesCreated;
    private int loadsStarted;
    private int loadsCancelled;
    private int serversDisposed;
    private int failLoads;
    private readonly Dictionary<McpCodeGraphServer, int> disposalCounts = new();

    internal int InstancesCreated => instancesCreated;

    internal int LoadsCancelled => loadsCancelled;

    internal int LoadsStarted => Volatile.Read(ref loadsStarted);

    internal int ServersDisposed => Volatile.Read(ref serversDisposed);

    internal Action<McpCodeGraphServer>? OnServerDisposed { get; set; }

    internal int DisposalsFor(McpCodeGraphServer server)
    {
        lock (disposalCounts)
        {
            return DisposalsForUnderLock(server);
        }
    }

    internal bool FailLoads
    {
        get => Volatile.Read(ref failLoads) == 1;
        set => Volatile.Write(ref failLoads, value ? 1 : 0);
    }

    internal Func<ProjectDefinition, ProjectInstanceCreation> Factory =>
        definition => ProjectInstanceCreation.Resident(CreateServer(definition));

    internal McpCodeGraphServer CreateServer(ProjectDefinition definition)
    {
        Interlocked.Increment(ref instancesCreated);
        return FailLoads ? CreateFailedLoadServer() : CreatePendingLoadServer();
    }

    internal McpCodeGraphServer CreatePendingLoadServer()
    {
        McpCodeGraphServer? server = null;
        server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = MinimalConfig(),
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                Interlocked.Increment(ref loadsStarted);
                var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() =>
                {
                    Interlocked.Increment(ref loadsCancelled);
                    RecordDisposal(server!);
                    pending.TrySetCanceled(token);
                });
                return pending.Task;
            },
        });
        return server;
    }

    private McpCodeGraphServer CreateFailedLoadServer()
    {
        McpCodeGraphServer? server = null;
        server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = MinimalConfig(),
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                Interlocked.Increment(ref loadsStarted);
                token.Register(() =>
                {
                    Interlocked.Increment(ref loadsCancelled);
                    RecordDisposal(server!);
                });
                return Task.FromException<SourceFileCatalog?>(new InvalidOperationException("Katalog kann nicht geladen werden."));
            },
        });
        return server;
    }

    private void RecordDisposal(McpCodeGraphServer server)
    {
        Interlocked.Increment(ref serversDisposed);
        lock (disposalCounts)
        {
            disposalCounts[server] = DisposalsForUnderLock(server) + 1;
        }

        OnServerDisposed?.Invoke(server);
    }

    private int DisposalsForUnderLock(McpCodeGraphServer server)
    {
        return disposalCounts.TryGetValue(server, out var count) ? count : 0;
    }

    private static Config MinimalConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };
}
