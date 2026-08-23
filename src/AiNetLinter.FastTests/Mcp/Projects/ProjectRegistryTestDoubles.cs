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

    internal int InstancesCreated => instancesCreated;

    internal int LoadsCancelled => loadsCancelled;

    internal int LoadsStarted => Volatile.Read(ref loadsStarted);

    internal int ServersDisposed => Volatile.Read(ref serversDisposed);

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
        return new McpCodeGraphServer(new McpCodeGraphServerOptions
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
                    Interlocked.Increment(ref serversDisposed);
                    pending.TrySetCanceled(token);
                });
                return pending.Task;
            },
        });
    }

    private McpCodeGraphServer CreateFailedLoadServer()
    {
        return new McpCodeGraphServer(new McpCodeGraphServerOptions
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
                    Interlocked.Increment(ref serversDisposed);
                });
                return Task.FromException<SourceFileCatalog?>(new InvalidOperationException("Katalog kann nicht geladen werden."));
            },
        });
    }

    private static Config MinimalConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };
}
