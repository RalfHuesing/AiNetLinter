#nullable enable

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

// Getaktete Daemone-Seite: Der Test beantwortet Handshake und Frames Schritt fuer
// Schritt selbst (kein Hintergrund-Skript), sodass Rohabbruch, Schweigen und
// Replay-Zeitpunkt exakt deterministisch sind.
[Trait("Category", "Integration")]
public sealed class ThinClientProxySessionContractTests
{
    private sealed class ScriptedServerQueue
    {
        private readonly List<DaemonPipeConnection> servers = [];
        private readonly object gate = new();

        public Func<CancellationToken, ValueTask<DaemonPipeConnection>> ConnectDelegate()
        {
            return _ =>
            {
                var (clientSide, daemonSide) = ThinClientPipeTestDoubles.CreateDuplexPair();
                lock (gate)
                {
                    servers.Add(new DaemonPipeConnection(daemonSide));
                    Monitor.Pulse(gate);
                }

                return ValueTask.FromResult(new DaemonPipeConnection(clientSide));
            };
        }

        public int Count
        {
            get { lock (gate) return servers.Count; }
        }

        public DaemonPipeConnection this[int index]
        {
            get { lock (gate) return servers[index]; }
        }

        public async Task<bool> WaitUntilCountAsync(int count, TimeSpan limit)
        {
            var deadline = DateTime.UtcNow + limit;
            while (DateTime.UtcNow < deadline)
            {
                if (Count >= count) return true;
                await Task.Delay(25).ConfigureAwait(false);
            }

            return Count >= count;
        }
    }

    [Fact]
    public async Task SecondRawFailure_ExitsWithoutThirdRound_AndReplaysExactlyOnce()
    {
        var servers = new ScriptedServerQueue();
        var console = new RecordingLintConsole();
        var spawnCount = 0;
        var stdin = new Pipe();
        var stdout = new Pipe();
        await stdin.Writer.WriteAsync("opaque-init\n"u8.ToArray()).ConfigureAwait(false);

        try
        {
            var session = ThinClientProxy.RunSessionAsync(
                CreateLaunchOptions(),
                CreateContext(console, servers.ConnectDelegate(), TimeSpan.FromSeconds(30),
                    (_, _) =>
                    {
                        Interlocked.Increment(ref spawnCount);
                        return false;
                    },
                    stdin.Reader.AsStream(),
                    stdout.Writer.AsStream()));

            var firstServer = await AwaitFirstServerAsync(servers).ConfigureAwait(false);
            await CompleteHandshakeAsync(firstServer, processId: 0, connectionId: 1).ConfigureAwait(false);

            // Opake Nutzbytes roh lesen — der validierende Connection-Reader ist
            // hier bewusst falsch, weil die Pump genau NICHTS interpretiert.
            var forwarded = await ReadRawFrameBoundedAsync(firstServer).ConfigureAwait(false);
            Assert.Equal("opaque-init", forwarded);

            // Kontrollierter Rohabbruch der ersten Verbindung.
            await firstServer.DisposeAsync().ConfigureAwait(false);

            Assert.True(
                await servers.WaitUntilCountAsync(2, TimeSpan.FromSeconds(10)).ConfigureAwait(false),
                $"Nach dem ersten Abbruch wurde keine zweite Verbindung aufgebaut: {string.Join(" || ", console.ErrorLines)}");
            var secondServer = servers[1];
            await CompleteHandshakeAsync(secondServer, processId: 0, connectionId: 2).ConfigureAwait(false);

            var replayed = await ReadRawFrameBoundedAsync(secondServer).ConfigureAwait(false);
            Assert.Equal("opaque-init", replayed);

            // Zweiter Rohabbruch: kein dritter Verbindungsversuch darf folgen.
            await secondServer.DisposeAsync().ConfigureAwait(false);

            var exitCode = await session.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            Assert.Equal(2, exitCode);
            Assert.Equal(2, servers.Count);
            Assert.Equal(0, Volatile.Read(ref spawnCount));
            Assert.Equal(2, console.ErrorLines.Count);
            Assert.Contains("[WARN]", console.ErrorLines[0], StringComparison.Ordinal);
            Assert.Contains("genau ein read-only Replay wird versucht", console.ErrorLines[0], StringComparison.Ordinal);
            Assert.Contains("kein weiterer Retry", console.ErrorLines[1], StringComparison.Ordinal);
            Assert.DoesNotContain(console.ErrorLines, line => line.Contains("Hanger-Schutz", StringComparison.Ordinal));
        }
        finally
        {
            await CompleteAsync(stdin).ConfigureAwait(false);
            await CompleteAsync(stdout).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task HangTimeout_KillsIdentifiedStandIn_EmitsSingleDistinguishableEvent()
    {
        using var standIn = new StandInProcess();
        var servers = new ScriptedServerQueue();
        var console = new RecordingLintConsole();
        var stdin = new Pipe();
        var stdout = new Pipe();

        try
        {
            var session = ThinClientProxy.RunSessionAsync(
                CreateLaunchOptions(),
                CreateContext(console, servers.ConnectDelegate(), TimeSpan.FromMilliseconds(500),
                    (_, _) => false,
                    stdin.Reader.AsStream(),
                    stdout.Writer.AsStream()));

            var firstServer = await AwaitFirstServerAsync(servers).ConfigureAwait(false);
            await CompleteHandshakeAsync(firstServer, standIn.Process.Id, connectionId: 1).ConfigureAwait(false);
            // Danach schweigt der Stellvertreter bewusst: der Pump-Idle-Timeout
            // ist der einzige Ausloeser fuer den Abbruch.

            Assert.True(
                await servers.WaitUntilCountAsync(2, TimeSpan.FromSeconds(10)).ConfigureAwait(false),
                "Nach dem Haenger-Timeout wurde kein Wiederholungsversuch gestartet.");
            var warning = Assert.Single(console.ErrorLines.ToList());
            Assert.Contains("[WARN]", warning, StringComparison.Ordinal);
            Assert.Contains("Hanger-Schutz-Zeitlimit", warning, StringComparison.Ordinal);
            Assert.True(
                await standIn.WaitForExitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false),
                "Der per Welcome-PID identifizierte Stellvertreterprozess wurde nicht beendet.");

            var secondServer = servers[1];
            await CompleteHandshakeAsync(secondServer, processId: 0, connectionId: 2).ConfigureAwait(false);
            await stdin.Writer.CompleteAsync().ConfigureAwait(false);

            var exitCode = await session.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

            Assert.Equal(0, exitCode);
            Assert.Equal(2, servers.Count);
            Assert.Single(console.ErrorLines);
        }
        finally
        {
            await CompleteAsync(stdin).ConfigureAwait(false);
            await CompleteAsync(stdout).ConfigureAwait(false);
        }
    }

    private static async Task<DaemonPipeConnection> AwaitFirstServerAsync(ScriptedServerQueue servers)
    {
        Assert.True(
            await servers.WaitUntilCountAsync(1, TimeSpan.FromSeconds(10)).ConfigureAwait(false),
            "Der Client hat keine Verbindung zum Mock-Daemonepunkt aufgebaut.");
        return servers[0];
    }

    private static async Task CompleteHandshakeAsync(
        DaemonPipeConnection server,
        int processId,
        int connectionId)
    {
        var hello = await server
            .ReadJsonFrameAsync<DaemonHello>()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        Assert.NotNull(hello);
        await MockDaemonScript.WriteWelcomeAsync(server, processId, connectionId).ConfigureAwait(false);
    }

    private static async Task<string> ReadRawFrameBoundedAsync(DaemonPipeConnection server) =>
        await ThinClientPipeTestDoubles
            .ReadFrameAsync(server.Stream)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);

    private static async Task CompleteAsync(Pipe pipe)
    {
        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
        await pipe.Reader.CompleteAsync().ConfigureAwait(false);
    }

    private static ThinClientLaunchOptions CreateLaunchOptions() =>
        new(null, null, null, null);

    private static ThinClientSessionContext CreateContext(
        RecordingLintConsole console,
        Func<CancellationToken, ValueTask<DaemonPipeConnection>> connectAsync,
        TimeSpan pumpIdleTimeout,
        Func<ThinClientLaunchOptions, Action<string>, bool> startDetached,
        Stream standardInput,
        Stream standardOutput) =>
        new(
            CancellationToken.None,
            console,
            new ThinClientSessionOptions(connectAsync, startDetached, pumpIdleTimeout, standardInput, standardOutput));
}
