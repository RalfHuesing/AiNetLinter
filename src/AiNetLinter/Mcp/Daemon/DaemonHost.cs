#nullable enable

using AiNetLinter.Output;
using System.Text.Json;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonHostOptions(
    IDaemonRegistry Registry,
    MruStateStore MruState,
    DaemonPipeTransport Transport,
    TimeProvider Clock,
    TimeSpan IdleExit,
    EffectiveDaemonConfiguration Configuration,
    ILintConsole Console,
    Func<DaemonPipeConnection, Task> SessionRunner,
    IDaemonIdentityProvider? IdentityProvider = null,
    TimeSpan? IdlePollInterval = null);

internal sealed class DaemonHost : IAsyncDisposable
{
    private static readonly TimeSpan DefaultIdlePollInterval = TimeSpan.FromSeconds(1);
    private readonly DaemonHostOptions options;
    private readonly object lifecycleGate = new();
    private readonly Dictionary<int, Task> connections = [];
    private readonly Dictionary<int, DaemonPipeConnection> connectionHandles = [];
    private readonly SemaphoreSlim warmupSlots = new(2, 2);
    private readonly CancellationTokenSource shutdownSource = new();
    private Task? warmupTask;
    private DateTimeOffset? idleSince;
    private int nextConnectionId;
    private int clientCount;
    private int activeWarmups;
    private int disposed;

    internal DaemonHost(DaemonHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.IdleExit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Die Idle-Exit-Zeit muss positiv sein.");
        }

        this.options = options;
        idleSince = options.Clock.GetUtcNow();
    }

    internal int ActiveConnectionCount
    {
        get
        {
            return Volatile.Read(ref clientCount);
        }
    }

    internal bool IsIdleExitDue()
    {
        lock (lifecycleGate)
        {
            if (connections.Count != 0 || idleSince is null) return false;
            if (activeWarmups != 0 || options.Registry.ActiveLoadCount != 0)
            {
                idleSince = options.Clock.GetUtcNow();
                return false;
            }

            return options.Clock.GetUtcNow() - idleSince >= options.IdleExit;
        }
    }

    internal void RegisterClientForTest()
    {
        RegisterClient();
    }

    internal void UnregisterClientForTest()
    {
        UnregisterClient();
    }

    internal Task WarmupForTestAsync(
        IReadOnlyList<MruStateEntry> candidates,
        CancellationToken cancellationToken = default) =>
        WarmupAsync(candidates, cancellationToken);

    internal async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            shutdownSource.Token);
        var daemonHandshake = new DaemonHandshake(
            options.IdentityProvider ?? new CurrentDaemonIdentityProvider(),
            options.Configuration);
        daemonHandshake.ConfigurationWarning += divergence =>
            options.Console.WriteError($"[WARN]: Daemon-Konfiguration weicht ab (erwartet {divergence.Expected.IdleExitMinutes} Minuten Idle-Exit).");

        var candidates = options.MruState.Read(options.Configuration.MaxProjects);
        warmupTask = WarmupAsync(candidates, linked.Token);
        var acceptTask = AcceptLoopAsync(daemonHandshake, linked.Token);
        var idleTask = IdleMonitorAsync(linked);
        var completed = await Task.WhenAny(acceptTask, idleTask).ConfigureAwait(false);
        if (completed == acceptTask && !linked.IsCancellationRequested)
        {
            linked.Cancel();
        }

        DisconnectSessions();
        await Task.WhenAll(acceptTask, idleTask).ConfigureAwait(false);
        await WaitForSessionsAsync().ConfigureAwait(false);
        if (warmupTask is not null)
        {
            await warmupTask.ConfigureAwait(false);
        }

        return await acceptTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        shutdownSource.Cancel();
        if (warmupTask is not null)
        {
            try
            {
                await warmupTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdownSource.IsCancellationRequested)
            {
            }
        }

        await options.Registry.DisposeAsync().ConfigureAwait(false);
        await options.MruState.DisposeAsync().ConfigureAwait(false);
        warmupSlots.Dispose();
        shutdownSource.Dispose();
    }

    private async Task<int> AcceptLoopAsync(DaemonHandshake handshake, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var connection = await options.Transport.AcceptAsync(cancellationToken).ConfigureAwait(false);
                RegisterConnection(connection, handshake);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
            catch (IOException exception)
            {
                options.Console.WriteError($"[ERROR]: Daemon-Pipe konnte nicht gebunden oder akzeptiert werden: {exception.Message}");
                return 1;
            }
        }

        return 0;
    }

    private void RegisterConnection(DaemonPipeConnection connection, DaemonHandshake handshake)
    {
        var connectionId = Interlocked.Increment(ref nextConnectionId);
        RegisterClient();
        var task = HandleConnectionAsync(connection, handshake, connectionId);
        lock (lifecycleGate)
        {
            connections[connectionId] = task;
            connectionHandles[connectionId] = connection;
        }
    }

    private async Task HandleConnectionAsync(
        DaemonPipeConnection connection,
        DaemonHandshake handshake,
        int connectionId)
    {
        try
        {
            await using var activeConnection = connection;
            var hello = await activeConnection.ReadJsonFrameAsync<DaemonHello>().ConfigureAwait(false);
            if (hello is null) return;

            var otherConnections = Math.Max(0, ActiveConnectionCount - 1);
            var result = handshake.HandleHello(hello, otherConnections);
            await WriteHandshakeResultAsync(activeConnection, result).ConfigureAwait(false);
            if (!result.IsAccepted) return;

            await options.SessionRunner(activeConnection).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            options.Console.WriteError($"[WARN]: Daemon-Verbindung {connectionId} wurde getrennt: {exception.Message}");
        }
        catch (InvalidDataException exception)
        {
            options.Console.WriteError($"[WARN]: Daemon-Verbindung {connectionId} enthielt einen ungueltigen Handshake: {exception.Message}");
        }
        catch (JsonException exception)
        {
            options.Console.WriteError($"[WARN]: Daemon-Verbindung {connectionId} enthielt kein gueltiges Handshake-JSON: {exception.Message}");
        }
        catch (Exception exception)
        {
            options.Console.WriteError($"[WARN]: Daemon-Verbindung {connectionId} wurde mit einem Sitzungsfehler beendet: {exception.Message}");
        }
        finally
        {
            lock (lifecycleGate)
            {
                connections.Remove(connectionId);
                connectionHandles.Remove(connectionId);
            }
            UnregisterClient();
        }
    }

    private static async Task WriteHandshakeResultAsync(
        DaemonPipeConnection connection,
        DaemonHandshakeResult result)
    {
        if (result.IsAccepted)
        {
            await connection.WriteJsonFrameAsync(result.Welcome).ConfigureAwait(false);
            return;
        }

        if (result.Status == DaemonHandshakeStatus.ShutdownRequested)
        {
            await connection.WriteJsonFrameAsync(result.Shutdown).ConfigureAwait(false);
            return;
        }

        await connection.WriteJsonFrameAsync(result.Error).ConfigureAwait(false);
    }

    private async Task IdleMonitorAsync(CancellationTokenSource linked)
    {
        var interval = options.IdlePollInterval is { } configured && configured > TimeSpan.Zero
            ? configured
            : DefaultIdlePollInterval;
        try
        {
            while (!linked.IsCancellationRequested)
            {
                TouchResidentProjects();
                if (IsIdleExitDue())
                {
                    linked.Cancel();
                    return;
                }

                await Task.Delay(interval, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
        }
    }

    private async Task WarmupAsync(
        IReadOnlyList<MruStateEntry> candidates,
        CancellationToken cancellationToken)
    {
        var tasks = candidates.Select(candidate => WarmupCandidateAsync(candidate, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task WarmupCandidateAsync(MruStateEntry candidate, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref activeWarmups);
        var slotAcquired = false;
        try
        {
            await warmupSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            slotAcquired = true;
            try
            {
                var result = options.Registry.Lease(candidate.RootPath);
                if (!result.Succeeded)
                {
                    options.MruState.Remove(candidate.RootPath);
                    return;
                }

                using var lease = result.Lease!;
                if (lease.LoadTask is { } loadTask)
                {
                    await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    lease.AdoptLoadedState();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                options.Console.WriteError($"[WARN]: MRU-Warmup fuer {candidate.RootPath} fehlgeschlagen: {exception.Message}");
            }
            finally
            {
                if (slotAcquired) warmupSlots.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Decrement(ref activeWarmups);
        }
    }

    private async Task WaitForSessionsAsync()
    {
        Task[] running;
        lock (lifecycleGate)
        {
            running = connections.Values.ToArray();
        }

        if (running.Length != 0)
        {
            await Task.WhenAll(running).ConfigureAwait(false);
        }
    }

    private void DisconnectSessions()
    {
        DaemonPipeConnection[] active;
        lock (lifecycleGate)
        {
            active = connectionHandles.Values.ToArray();
        }

        foreach (var connection in active)
        {
            connection.Disconnect();
        }
    }

    private void RegisterClient()
    {
        if (Interlocked.Increment(ref clientCount) == 1)
        {
            lock (lifecycleGate)
            {
                idleSince = null;
            }
        }
    }

    private void UnregisterClient()
    {
        if (Interlocked.Decrement(ref clientCount) == 0)
        {
            lock (lifecycleGate)
            {
                idleSince = options.Clock.GetUtcNow();
            }
        }
    }

    private void TouchResidentProjects()
    {
        foreach (var snapshot in options.Registry.Snapshots())
        {
            options.MruState.Touch(snapshot.RootPath, snapshot.LastUsedUtc);
        }
    }
}
