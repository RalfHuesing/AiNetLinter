#nullable enable

using AiNetLinter.Output;
using System.Text.Json;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonHostOptions(
    IDaemonRegistry Registry,
    MruStateStore MruState,
    IDaemonPipeTransport Transport,
    TimeProvider Clock,
    TimeSpan IdleExit,
    EffectiveDaemonConfiguration Configuration,
    ILintConsole Console,
    Func<DaemonPipeConnection, Task> SessionRunner,
    IDaemonIdentityProvider? IdentityProvider = null,
    TimeSpan? IdlePollInterval = null,
    IDaemonInstanceLock? InstanceLock = null);

internal sealed class DaemonHost : IAsyncDisposable
{
    private static readonly TimeSpan DefaultIdlePollInterval = TimeSpan.FromSeconds(1);
    private readonly DaemonHostOptions options;
    private readonly IDaemonInstanceLock instanceLock;
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
    private readonly DateTimeOffset startedAt;

    internal DaemonHost(DaemonHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.IdleExit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Die Idle-Exit-Zeit muss positiv sein.");
        }

        this.options = options;
        instanceLock = options.InstanceLock ?? new DaemonInstanceLock(options.Transport.Endpoint.PipeName);
        startedAt = options.Clock.GetUtcNow();
        idleSince = startedAt;
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
        if (!TryAcquireInstanceLock())
        {
            return 1;
        }

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
        instanceLock.Dispose();
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
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (lifecycleGate)
        {
            RegisterClientLocked();
            connections[connectionId] = completion.Task;
            connectionHandles[connectionId] = connection;
        }

        _ = HandleConnectionAsync(connection, handshake, connectionId, completion);
    }

    private async Task HandleConnectionAsync(
        DaemonPipeConnection connection,
        DaemonHandshake handshake,
        int connectionId,
        TaskCompletionSource completion)
    {
        try
        {
            await using var activeConnection = connection;
            var hello = await activeConnection.ReadJsonFrameAsync<DaemonHello>().ConfigureAwait(false);
            if (hello is null) return;

            var otherConnections = Math.Max(0, ActiveConnectionCount - 1);
            var result = handshake.HandleHello(hello, otherConnections);
            if (result.IsAccepted && result.Welcome is not null)
            {
                result = result with
                {
                    Welcome = result.Welcome with
                    {
                        ConnectionId = connectionId,
                    },
                };
            }

            await WriteHandshakeResultAsync(activeConnection, result).ConfigureAwait(false);
            if (result.Status == DaemonHandshakeStatus.ShutdownRequested)
            {
                shutdownSource.Cancel();
            }

            if (!result.IsAccepted) return;

            activeConnection.RuntimeContext = CreateRuntimeContext(connectionId);
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
            CompleteConnection(connectionId, completion);
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
        lock (lifecycleGate)
        {
            RegisterClientLocked();
        }
    }

    private void UnregisterClient()
    {
        lock (lifecycleGate)
        {
            if (clientCount == 0)
            {
                return;
            }

            clientCount--;
            if (clientCount == 0)
            {
                idleSince = options.Clock.GetUtcNow();
            }
        }
    }

    private bool TryAcquireInstanceLock()
    {
        try
        {
            if (instanceLock.TryAcquire())
            {
                return true;
            }

            options.Console.WriteError(
                $"[ERROR]: Daemon fuer Pipe-Endpunkt '{options.Transport.Endpoint.PipeName}' laeuft bereits.");
            return false;
        }
        catch (Exception exception)
        {
            options.Console.WriteError($"[ERROR]: Daemon-Lock konnte nicht erworben werden: {exception.Message}");
            return false;
        }
    }

    private void RegisterClientLocked()
    {
        clientCount++;
        idleSince = null;
    }

    private void CompleteConnection(int connectionId, TaskCompletionSource completion)
    {
        bool wasRegistered;
        lock (lifecycleGate)
        {
            wasRegistered = connections.Remove(connectionId) || connectionHandles.Remove(connectionId);
            connections.Remove(connectionId);
            connectionHandles.Remove(connectionId);
        }

        if (wasRegistered)
        {
            UnregisterClient();
        }

        completion.TrySetResult();
    }

    private void TouchResidentProjects()
    {
        foreach (var snapshot in options.Registry.Snapshots())
        {
            options.MruState.Touch(snapshot.RootPath, snapshot.LastUsedUtc);
        }
    }

    private DaemonRuntimeContext CreateRuntimeContext(int connectionId) =>
        new(connectionId, SnapshotRuntime);

    private DaemonRuntimeSnapshot SnapshotRuntime()
    {
        var keys = options.Registry.Snapshots().Select(snapshot => snapshot.RootPath).ToArray();
        return new DaemonRuntimeSnapshot(
            ActiveConnectionCount,
            Environment.ProcessId,
            options.Clock.GetUtcNow() - startedAt,
            keys,
            McpServerOptionsFactory.GetServerVersion());
    }
}
