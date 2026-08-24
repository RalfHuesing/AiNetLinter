#nullable enable

using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class DaemonHostLifecycleTests
{
    [Fact]
    public async Task IdleExit_RequiresNoClientsAndConfiguredIdleDuration()
    {
        using var temp = TestTempDirectory.Create("daemon-host-");
        var clock = new DaemonTestClock();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(clock);
        await using var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), clock));
        await using var host = CreateHost(new DaemonRegistryAdapter(registry), mru, clock, TimeSpan.FromMinutes(10));

        host.RegisterClientForTest();
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.False(host.IsIdleExitDue());

        host.UnregisterClientForTest();
        Assert.False(host.IsIdleExitDue());
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.False(host.IsIdleExitDue());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(host.IsIdleExitDue());
    }

    [Fact]
    public async Task IdleExit_RestartsIdleWindowAfterActiveLoadCompletes()
    {
        using var temp = TestTempDirectory.Create("daemon-host-");
        var clock = new DaemonTestClock();
        var registry = new StubDaemonRegistry { ActiveLoadCount = 1 };
        await using var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), clock));
        await using var host = CreateHost(registry, mru, clock, TimeSpan.FromMinutes(1));

        host.RegisterClientForTest();
        host.UnregisterClientForTest();
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(host.IsIdleExitDue());

        registry.ActiveLoadCount = 0;
        Assert.False(host.IsIdleExitDue());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(host.IsIdleExitDue());
    }

    private static DaemonHost CreateHost(
        IDaemonRegistry registry,
        MruStateStore mru,
        TimeProvider clock,
        TimeSpan idleExit) =>
        new(new DaemonHostOptions(
            registry,
            mru,
            new DaemonPipeTransport(() => "daemon-host-tests"),
            clock,
            idleExit,
            new EffectiveDaemonConfiguration(4, (decimal)idleExit.TotalMinutes, "stderr"),
            LinterConsole.Instance,
            _ => Task.CompletedTask));

    private sealed class DaemonTestClock : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);

        public void Advance(TimeSpan delta) => Interlocked.Add(ref ticks, delta.Ticks);
    }

    private sealed class StubDaemonRegistry : IDaemonRegistry
    {
        public int ActiveLoadCount { get; set; }

        public IReadOnlyList<DaemonProjectSnapshot> Snapshots() => [];

        public DaemonRegistryLeaseResult Lease(string rootPath) => new(null, "not used");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
