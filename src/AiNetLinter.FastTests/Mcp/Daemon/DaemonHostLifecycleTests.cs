#nullable enable

using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using System.Text;

namespace AiNetLinter.FastTests.Mcp.Daemon;

// @covers DaemonHost
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

    [Fact]
    public async Task RunAsync_QuickEofCleansConnectionBeforeIdleExitAndReleasesLock()
    {
        using var temp = TestTempDirectory.Create("daemon-host-");
        var lockSeam = new TrackingInstanceLock();
        var transport = new ControlledDaemonTransport(new DaemonPipeConnection(new MemoryStream()));
        var clock = TimeProvider.System;
        await using var registry = new StubDaemonRegistry();
        var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), clock));
        var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            transport,
            clock,
            TimeSpan.FromMilliseconds(10),
            EffectiveDaemonConfiguration.Default,
            LinterConsole.Instance,
            _ => Task.CompletedTask,
            new FakeIdentityProvider(),
            TimeSpan.FromMilliseconds(1),
            lockSeam));

        Assert.Equal(0, await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(0, host.ActiveConnectionCount);

        await host.DisposeAsync();
        Assert.True(lockSeam.Acquired);
        Assert.True(lockSeam.Released);
        Assert.True(File.Exists(temp.GetPath("state.json")));
    }

    [Fact]
    public async Task RunAsync_WhenInstanceLockIsHeld_ReturnsNonZeroBeforeAccepting()
    {
        using var temp = TestTempDirectory.Create("daemon-host-");
        var transport = new ControlledDaemonTransport(new DaemonPipeConnection(new MemoryStream()));
        var console = new RecordingLintConsole();
        await using var registry = new StubDaemonRegistry();
        await using var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), TimeProvider.System));
        await using var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            transport,
            TimeProvider.System,
            TimeSpan.FromMinutes(1),
            EffectiveDaemonConfiguration.Default,
            console,
            _ => Task.CompletedTask,
            InstanceLock: new TrackingInstanceLock(acquireResult: false)));

        Assert.Equal(1, await host.RunAsync());
        Assert.Equal(0, transport.AcceptCount);
        Assert.Contains("laeuft bereits", console.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WritesHandshakeBeforeStartingSessionRunner()
    {
        using var temp = TestTempDirectory.Create("daemon-host-");
        var hello = DaemonPipeTransport.SerializeFrame(new DaemonHello(
            "exe-1",
            7,
            EffectiveDaemonConfiguration.Default));
        var input = new MemoryStream();
        input.Write(hello);
        input.Write("\n"u8);
        input.Position = 0;
        var transport = new ControlledDaemonTransport(new DaemonPipeConnection(input));
        var sessionStarted = false;
        var welcomeWasWritten = false;
        var console = new RecordingLintConsole();
        var clock = TimeProvider.System;
        await using var registry = new StubDaemonRegistry();
        await using var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), clock));
        await using var host = new DaemonHost(new DaemonHostOptions(
            registry,
            mru,
            transport,
            clock,
            TimeSpan.FromMilliseconds(10),
            EffectiveDaemonConfiguration.Default,
            console,
            connection =>
            {
                sessionStarted = true;
                welcomeWasWritten = Encoding.UTF8.GetString(input.ToArray()).Contains("welcome", StringComparison.Ordinal);
                return Task.CompletedTask;
            },
            new FakeIdentityProvider(),
            TimeSpan.FromMilliseconds(1),
            new TrackingInstanceLock()));

        Assert.Equal(0, await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(sessionStarted, console.ErrorText);
        Assert.True(welcomeWasWritten);
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
            new EffectiveDaemonConfiguration(4, (decimal)idleExit.TotalMinutes),
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

    private sealed class ControlledDaemonTransport(DaemonPipeConnection connection) : IDaemonPipeTransport
    {
        private int served;

        public int AcceptCount { get; private set; }

        public DaemonPipeEndpoint Endpoint { get; } = DaemonPipeEndpoint.ForUser("daemon-host-contracts");

        public ValueTask<DaemonPipeConnection> AcceptAsync(CancellationToken cancellationToken)
        {
            AcceptCount++;
            if (Interlocked.Exchange(ref served, 1) == 0)
            {
                return ValueTask.FromResult(connection);
            }

            return new ValueTask<DaemonPipeConnection>(WaitForCancellationAsync(cancellationToken));
        }

        private static async Task<DaemonPipeConnection> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Der kontrollierte Transport sollte nur per Cancellation enden.");
        }
    }

    private sealed class TrackingInstanceLock(bool acquireResult = true) : IDaemonInstanceLock
    {
        public bool Acquired { get; private set; }

        public bool Released { get; private set; }

        public bool TryAcquire()
        {
            Acquired = true;
            return acquireResult;
        }

        public void Dispose() => Released = true;
    }

    private sealed class FakeIdentityProvider : IDaemonIdentityProvider
    {
        public DaemonIdentity GetIdentity() => new("daemon-1", "exe-1", 99);
    }
}
