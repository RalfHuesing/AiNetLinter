#nullable enable

using System.IO.Pipelines;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class ThinClientConnectOrStartTests
{
    private const string DaemonConfigurationWarning = "Daemon-Konfiguration";

    [Fact]
    public async Task ConnectOrStart_UsesExistingMockPipeWithoutSpawn()
    {
        var transport = new ScriptedMockPipeTransport(initialConnectFailures: 0);
        var console = new RecordingLintConsole();
        var spawnCount = 0;

        var connection = await ThinClientProxy.ConnectOrStartAsync(
            CreateLaunchOptions(),
            CreateContext(console, transport, (_, _) =>
            {
                Interlocked.Increment(ref spawnCount);
                return true;
            })).ConfigureAwait(false);

        Assert.Equal(4711, connection.ProcessId);
        Assert.Equal(1, transport.ConnectAttempts);
        Assert.Equal(0, Volatile.Read(ref spawnCount));
    }

    [Fact]
    public async Task ConnectOrStart_SpawnsDetachedOnceAndRetriesUntilWelcome()
    {
        var transport = new ScriptedMockPipeTransport(initialConnectFailures: 2);
        var console = new RecordingLintConsole();
        var spawnCount = 0;

        var connection = await ThinClientProxy.ConnectOrStartAsync(
            CreateLaunchOptions(),
            CreateContext(console, transport, (_, _) =>
            {
                Interlocked.Increment(ref spawnCount);
                return true;
            })).ConfigureAwait(false);

        Assert.Equal(4711, connection.ProcessId);
        Assert.Equal(1, Volatile.Read(ref spawnCount));
        Assert.Equal(3, transport.ConnectAttempts);
        Assert.Equal(
            1,
            console.ErrorLines.Count(line => line.Contains("[INFO]: Daemon-Connect-first fehlgeschlagen", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ConnectOrStart_PropagatesStartFailureWithoutReadinessLoop()
    {
        var transport = new ScriptedMockPipeTransport(initialConnectFailures: int.MaxValue);
        var console = new RecordingLintConsole();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            ThinClientProxy.ConnectOrStartAsync(
                CreateLaunchOptions(),
                CreateContext(console, transport, (_, _) => false))).ConfigureAwait(false);

        Assert.Contains("noch nicht bereit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, transport.ConnectAttempts);
    }

    [Fact]
    public async Task ConnectOrStart_ConcurrentStartersConvergeOnSingleMockPipe()
    {
        using var startupGate = new TestStartupGate();
        var spawnCount = 0;
        var transport = new ScriptedMockPipeTransport(
            initialConnectFailures: 2,
            acceptWhen: () => Volatile.Read(ref spawnCount) >= 1);
        var console = new RecordingLintConsole();
        bool StartDetached(ThinClientLaunchOptions _, Action<string> __)
        {
            Interlocked.Increment(ref spawnCount);
            return true;
        }

        var context = CreateContext(console, transport, StartDetached, startupGate.AcquireAsync);
        var connections = await Task.WhenAll(
            ThinClientProxy.ConnectOrStartAsync(CreateLaunchOptions(), context),
            ThinClientProxy.ConnectOrStartAsync(CreateLaunchOptions(), context)).ConfigureAwait(false);

        Assert.All(connections, connection => Assert.Equal(4711, connection.ProcessId));
        Assert.NotEqual(connections[0].ConnectionId, connections[1].ConnectionId);
        Assert.Equal(1, Volatile.Read(ref spawnCount));
        Assert.True(transport.ConnectAttempts > 2, $"Erwartete mehr als zwei Verbindungsversuche, gemessen {transport.ConnectAttempts}.");
    }

    [Fact]
    public async Task ConnectOrStart_ForwardsInstanceToTransportAndStartupGate()
    {
        var transport = new ScriptedMockPipeTransport(initialConnectFailures: 2);
        var console = new RecordingLintConsole();
        string? connectedInstance = null;
        string? gatedInstance = null;
        using var startupGate = new TestStartupGate();

        var session = new ThinClientSessionOptions(
            transport.ConnectAsync,
            (_, _) => true,
            TimeSpan.FromSeconds(30),
            new Pipe().Reader.AsStream(),
            new Pipe().Writer.AsStream(),
            ConnectForInstanceAsync: (instance, cancellationToken) =>
            {
                connectedInstance = instance;
                return transport.ConnectAsync(cancellationToken);
            },
            AcquireStartupGateForInstanceAsync: (cancellationToken, timeout, instance) =>
            {
                gatedInstance = instance;
                return startupGate.AcquireAsync(cancellationToken, timeout);
            });

        var context = new ThinClientSessionContext(CancellationToken.None, console, session);
        var connection = await ThinClientProxy.ConnectOrStartAsync(
            new ThinClientLaunchOptions(null, null, null, "BETA"),
            context).ConfigureAwait(false);
        try
        {
            Assert.Equal("beta", connectedInstance);
            Assert.Equal("beta", gatedInstance);
        }
        finally
        {
            await connection.Pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task ConnectOrStart_RejectsLegacySeamsForInstanceInsteadOfUsingDefaultPipeOrGate()
    {
        var transport = new ScriptedMockPipeTransport(initialConnectFailures: 0);
        var console = new RecordingLintConsole();
        var spawnCount = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ThinClientProxy.ConnectOrStartAsync(
                new ThinClientLaunchOptions(null, null, null, "beta"),
                CreateContext(console, transport, (_, _) =>
                {
                    Interlocked.Increment(ref spawnCount);
                    return true;
                }))).ConfigureAwait(false);

        Assert.Contains("ConnectForInstanceAsync", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, transport.ConnectAttempts);
        Assert.Equal(0, Volatile.Read(ref spawnCount));
    }

    [Fact]
    public async Task ConnectOrStart_ReportsExternalLimitDivergenceFromExistingDaemon()
    {
        var daemonConfiguration = new EffectiveDaemonConfiguration(
            4,
            10m,
            ExternalMaxDiskBytes: 100,
            ExternalMaxMemoryBytes: 200,
            ExternalMaxParallelOperations: 3,
            ExternalMaxResidentResources: 5,
            ExternalIdleTtlMinutes: 12m);
        var transport = new ScriptedMockPipeTransport(
            initialConnectFailures: 0,
            serveConnection: (connection, index) => MockDaemonScript.WelcomeThenAsync(
                connection,
                4711,
                index,
                _ => Task.Delay(Timeout.InfiniteTimeSpan),
                daemonConfiguration));
        var console = new RecordingLintConsole();
        var options = new ThinClientLaunchOptions(
            null,
            null,
            null,
            null,
            ExternalMaxDiskBytes: 50,
            ExternalMaxMemoryBytes: 200,
            ExternalMaxParallelOperations: 3,
            ExternalMaxResidentResources: 5,
            ExternalIdleTtlMinutes: 12m);

        var connection = await ThinClientProxy.ConnectOrStartAsync(
            options,
            CreateContext(console, transport, (_, _) => false)).ConfigureAwait(false);
        try
        {
            Assert.Contains(
                console.ErrorLines,
                line => line.Contains(DaemonConfigurationWarning, StringComparison.Ordinal));
        }
        finally
        {
            await connection.Pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task ConnectOrStart_AcceptsExternalIdleTtlWithSameNormalizedTicks()
    {
        var daemonConfiguration = new EffectiveDaemonConfiguration(
            4,
            10m,
            ExternalIdleTtlMinutes: 1m);
        var transport = new ScriptedMockPipeTransport(
            initialConnectFailures: 0,
            serveConnection: (connection, index) => MockDaemonScript.WelcomeThenAsync(
                connection,
                4711,
                index,
                _ => Task.Delay(Timeout.InfiniteTimeSpan),
                daemonConfiguration));
        var console = new RecordingLintConsole();
        var options = new ThinClientLaunchOptions(
            null,
            null,
            null,
            null,
            ExternalIdleTtlMinutes: 1.0000000001m);

        var connection = await ThinClientProxy.ConnectOrStartAsync(
            options,
            CreateContext(console, transport, (_, _) => false)).ConfigureAwait(false);
        try
        {
            Assert.Equal(TimeSpan.FromMinutes(1d).Ticks, TimeSpan.FromMinutes(1.0000000001d).Ticks);
            Assert.DoesNotContain(
                console.ErrorLines,
                line => line.Contains(DaemonConfigurationWarning, StringComparison.Ordinal));
        }
        finally
        {
            await connection.Pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task ConnectOrStart_ReportsExternalIdleTtlWithDifferentNormalizedTicks()
    {
        var daemonConfiguration = new EffectiveDaemonConfiguration(
            4,
            10m,
            ExternalIdleTtlMinutes: 1m);
        var transport = new ScriptedMockPipeTransport(
            initialConnectFailures: 0,
            serveConnection: (connection, index) => MockDaemonScript.WelcomeThenAsync(
                connection,
                4711,
                index,
                _ => Task.Delay(Timeout.InfiniteTimeSpan),
                daemonConfiguration));
        var console = new RecordingLintConsole();
        var options = new ThinClientLaunchOptions(
            null,
            null,
            null,
            null,
            ExternalIdleTtlMinutes: 1.000000002m);

        var connection = await ThinClientProxy.ConnectOrStartAsync(
            options,
            CreateContext(console, transport, (_, _) => false)).ConfigureAwait(false);
        try
        {
            Assert.NotEqual(TimeSpan.FromMinutes(1d).Ticks, TimeSpan.FromMinutes(1.000000002d).Ticks);
            Assert.Contains(
                console.ErrorLines,
                line => line.Contains(DaemonConfigurationWarning, StringComparison.Ordinal));
        }
        finally
        {
            await connection.Pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ThinClientLaunchOptions CreateLaunchOptions() =>
        new(null, null, null);

    private static ThinClientSessionContext CreateContext(
        RecordingLintConsole console,
        ScriptedMockPipeTransport transport,
        Func<ThinClientLaunchOptions, Action<string>, bool> startDetached,
        Func<CancellationToken, TimeSpan, ValueTask<IAsyncDisposable>>? acquireStartupGateAsync = null)
    {
        var idleInput = new Pipe();
        var idleOutput = new Pipe();
        var session = new ThinClientSessionOptions(
            transport.ConnectAsync,
            startDetached,
            TimeSpan.FromSeconds(30),
            idleInput.Reader.AsStream(),
            idleOutput.Writer.AsStream(),
            acquireStartupGateAsync);
        return new ThinClientSessionContext(CancellationToken.None, console, session);
    }

    private sealed class TestStartupGate : IDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        internal async ValueTask<IAsyncDisposable> AcquireAsync(
            CancellationToken cancellationToken,
            TimeSpan _)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Lease(gate);
        }

        public void Dispose() => gate.Dispose();

        private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
        {
            private int disposed;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    gate.Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
