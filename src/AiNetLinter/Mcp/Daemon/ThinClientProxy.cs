#nullable enable

using System.Text.Json;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Mcp.Lifetime;
using AiNetLinter.Output;
using Serilog;

namespace AiNetLinter.Mcp.Daemon;

// Kollaborateure einer Thin-Client-Sitzung: Der Default baut Produktions-
// Transport, detached Spawn und Konsolen-Streams; Tests injizieren hier,
// um Connect-or-Start, Retry-Fenster und Haenger-Pfad deterministisch zu fahren.
internal sealed record ThinClientSessionOptions(
    Func<CancellationToken, ValueTask<DaemonPipeConnection>> ConnectAsync,
    Func<ThinClientLaunchOptions, Action<string>, bool> StartDetached,
    TimeSpan PumpIdleTimeout,
    Stream StandardInput,
    Stream StandardOutput,
    Func<CancellationToken, TimeSpan, ValueTask<IAsyncDisposable>>? AcquireStartupGateAsync = null)
{
    internal static ThinClientSessionOptions Default(TimeSpan pumpIdleTimeout) => new(
        cancellationToken => new DaemonPipeTransport().ConnectAsync(cancellationToken),
        ThinClientLauncher.TryStartDetached,
        pumpIdleTimeout,
        Console.OpenStandardInput(),
        Console.OpenStandardOutput(),
        DaemonStartupGate.AcquireAsync);
}

internal sealed record ThinClientSessionContext(
    CancellationToken Token,
    ILintConsole Console,
    ThinClientSessionOptions Session);

internal sealed record ThinClientConnection(DaemonPipeConnection Pipe, int ProcessId, int ConnectionId);

internal static class ThinClientProxy
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(15);
    // Eine MCP-Verbindung darf ohne Wire-Aktivitaet idle bleiben. EOF und der
    // Parent-Watchdog bleiben die Produktions-Beendigungssignale; ein Pump-
    // Timeout ist nur ueber explizite Test-/Sonderpfad-Optionen aktiv.
    internal static readonly TimeSpan DefaultPumpIdleTimeout = TimeSpan.Zero;
    private const int MaximumRetries = 1;

    internal static async Task<int> RunAsync(
        LinterArgs args,
        CancellationToken cancellationToken = default,
        ILintConsole? console = null)
    {
        var clientConsole = console ?? LinterConsole.Instance;
        if (IsEscapeEnabled())
        {
            Log.Debug("ThinClient: Debug-Escape aktiv (AINETLINTER_NO_DAEMON=1) - direkter In-Proc-Stdio-Pfad ohne Daemon");
            return await McpServerCommand.RunAsync(args, cancellationToken, clientConsole).ConfigureAwait(false);
        }

        var validationError = args.Validate();
        if (validationError is not null)
        {
            clientConsole.WriteError(validationError);
            return 1;
        }

        await using var lifetime = McpServerLifetime.Start(args.ParentPid, cancellationToken, clientConsole.WriteError);
        var session = ThinClientSessionOptions.Default(DefaultPumpIdleTimeout);
        var context = new ThinClientSessionContext(lifetime.Token, clientConsole, session);
        return await RunSessionAsync(CreateLaunchOptions(args), context).ConfigureAwait(false);
    }

    internal static async Task<int> RunSessionAsync(
        ThinClientLaunchOptions launchOptions,
        ThinClientSessionContext context)
    {
        var replayFrame = (byte[]?)null;
        try
        {
            for (var attempt = 0; attempt <= MaximumRetries; attempt++)
            {
                var connection = await ConnectOrStartAsync(launchOptions, context).ConfigureAwait(false);
                try
                {
                    var pumpResult = await DaemonBytePump.RunAsync(
                        context.Session.StandardInput,
                        context.Session.StandardOutput,
                        connection.Pipe.Stream,
                        new DaemonPumpOptions(context.Session.PumpIdleTimeout, replayFrame),
                        context.Token).ConfigureAwait(false);
                    Log.Information("ThinClient: Pump beendet (Completed={Completed}, Attempt={Attempt}, DaemonPid={DaemonPid}, ConnectionId={ConnectionId})", pumpResult.Completed, attempt, connection.ProcessId, connection.ConnectionId);
                    if (pumpResult.Completed) return 0;

                    ReportPumpFailure(context.Console, attempt, connection, pumpResult.Failure);
                    replayFrame = pumpResult.ReplayFrame;
                    if (attempt == MaximumRetries)
                    {
                        Log.Error("ThinClient: Kein Retry mehr moeglich, ExitCode=2 (Replay-Fenster={HasReplayFrame})", replayFrame is not null);
                        return 2;
                    }

                }
                finally
                {
                    await connection.Pipe.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (context.Token.IsCancellationRequested)
        {
            Log.Information("ThinClient: Session durch Client-Abbruch beendet, ExitCode=0");
            return 0;
        }

        Log.Error("ThinClient: Session unerwartet beendet, ExitCode=2");
        return 2;
    }

    private static ThinClientLaunchOptions CreateLaunchOptions(LinterArgs args) => new(
        args.McpProjectTtlMinutes,
        args.McpMaxProjects,
        args.McpDaemonIdleExitMinutes);

    internal static async Task<ThinClientConnection> ConnectOrStartAsync(
        ThinClientLaunchOptions options,
        ThinClientSessionContext context)
    {
        var firstAttempt = await TryConnectFirstAsync(options, context).ConfigureAwait(false);
        if (firstAttempt.Connection is not null)
        {
            Log.Information("ThinClient: Mit bestehendem Daemon verbunden (DaemonPid={DaemonPid}, ConnectionId={ConnectionId})", firstAttempt.Connection.ProcessId, firstAttempt.Connection.ConnectionId);
            return firstAttempt.Connection;
        }

        Log.Information("ThinClient: Kein Daemon erreichbar ({Reason}) - starte detached", firstAttempt.Failure?.GetType().Name ?? "unbekannt");
        var acquireStartupGate = context.Session.AcquireStartupGateAsync ?? DaemonStartupGate.AcquireAsync;
        await using var startupGate = await acquireStartupGate(context.Token, ReadinessTimeout).ConfigureAwait(false);

        var recheck = await TryConnectFirstAsync(options, context, reportFailure: false).ConfigureAwait(false);
        if (recheck.Connection is not null)
        {
            Log.Information("ThinClient: Mit bestehendem Daemon verbunden (DaemonPid={DaemonPid}, ConnectionId={ConnectionId})", recheck.Connection.ProcessId, recheck.Connection.ConnectionId);
            return recheck.Connection;
        }

        if (!context.Session.StartDetached(options, context.Console.WriteError))
        {
            Log.Error("ThinClient: Detached-Start fehlgeschlagen, ExitCode=2");
            throw firstAttempt.Failure ?? new IOException("Der Daemon konnte nicht gestartet werden.");
        }

        var connection = await WaitForReadinessAsync(options, context).ConfigureAwait(false);
        Log.Information("ThinClient: Daemon nach Start bereit (DaemonPid={DaemonPid}, ConnectionId={ConnectionId})", connection.ProcessId, connection.ConnectionId);
        return connection;
    }

    private static async Task<ConnectAttempt> TryConnectFirstAsync(
        ThinClientLaunchOptions options,
        ThinClientSessionContext context,
        bool reportFailure = true)
    {
        try
        {
            var connection = await ConnectAsync(options, ConnectTimeout, context.Token, context).ConfigureAwait(false);
            return new ConnectAttempt(connection, null);
        }
        catch (ThinClientVersionConflictException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (reportFailure)
            {
                context.Console.WriteError($"[INFO]: Daemon-Connect-first fehlgeschlagen: {exception.Message}");
            }

            return new ConnectAttempt(null, exception);
        }
    }

    private static async Task<ThinClientConnection> WaitForReadinessAsync(
        ThinClientLaunchOptions options,
        ThinClientSessionContext context)
    {
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        readiness.CancelAfter(ReadinessTimeout);
        while (true)
        {
            try
            {
                return await ConnectAsync(options, ConnectTimeout, readiness.Token, context).ConfigureAwait(false);
            }
            catch (ThinClientVersionConflictException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await DelayReadinessRetryAsync(readiness, context.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                await DelayReadinessRetryAsync(readiness, context.Token).ConfigureAwait(false);
            }
        }
    }

    private static async Task DelayReadinessRetryAsync(
        CancellationTokenSource readiness,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
        if (readiness.IsCancellationRequested)
        {
            throw new TimeoutException("Daemon-Readiness-Handshake hat das Zeitlimit ueberschritten.");
        }

        await Task.Delay(ConnectTimeout, readiness.Token).ConfigureAwait(false);
    }

    private static async Task<ThinClientConnection> ConnectAsync(
        ThinClientLaunchOptions options,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        ThinClientSessionContext context)
    {
        using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connect.CancelAfter(timeout);
        var pipe = await context.Session.ConnectAsync(connect.Token).ConfigureAwait(false);
        try
        {
            var hello = new DaemonHello(
                McpServerOptionsFactory.GetServerVersion(),
                Environment.ProcessId,
                CreateConfiguration(options));
            await pipe.WriteJsonFrameAsync(hello, connect.Token).ConfigureAwait(false);
            var response = await pipe.ReadFrameAsync(connect.Token).ConfigureAwait(false)
                ?? throw new EndOfStreamException("Daemon beendete den Handshake ohne Antwort.");
            return ReadHandshakeResponse(pipe, response, CreateConfiguration(options), context.Console);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static ThinClientConnection ReadHandshakeResponse(
        DaemonPipeConnection pipe,
        byte[] response,
        EffectiveDaemonConfiguration expectedConfiguration,
        ILintConsole console)
    {
        using var document = JsonDocument.Parse(response);
        var type = document.RootElement.TryGetProperty("type", out var typeValue)
            ? typeValue.GetString()
            : null;
        if (string.Equals(type, DaemonProtocol.Welcome, StringComparison.Ordinal))
        {
            var welcome = JsonSerializer.Deserialize<DaemonWelcome>(response, DaemonProtocol.JsonOptions)
                ?? throw new InvalidDataException("Daemon-Welcome konnte nicht gelesen werden.");
            Log.Debug("ThinClient: Handshake Welcome empfangen (DaemonPid={DaemonPid}, DaemonVersion={DaemonVersion}, ExecutableVersion={ExecutableVersion}, IdleExit={IdleExitMinutes}, MaxProjects={MaxProjects})", welcome.ProcessId, welcome.DaemonVersion, welcome.ExecutableVersion, welcome.Configuration.IdleExitMinutes, welcome.Configuration.MaxProjects);
            if (!expectedConfiguration.Matches(welcome.Configuration))
            {
                console.WriteError("[WARN]: Daemon-Konfiguration weicht von den Client-Flags ab.");
            }

            return new ThinClientConnection(pipe, welcome.ProcessId, welcome.ConnectionId);
        }

        if (string.Equals(type, DaemonProtocol.Shutdown, StringComparison.Ordinal))
        {
            throw new IOException("Daemon meldet kontrollierten Neustart nach Versionskonflikt.");
        }

        var error = JsonSerializer.Deserialize<DaemonError>(response, DaemonProtocol.JsonOptions);
        if (error?.Code == DaemonProtocol.VersionConflict)
        {
            Log.Error("ThinClient: Handshake abgelehnt - Versionskonflikt ({Message})", error.Message);
            throw new ThinClientVersionConflictException(error.Message);
        }

        if (error?.Code == DaemonProtocol.UnsupportedProtocolVersion)
        {
            Log.Error("ThinClient: Handshake abgelehnt - Protokollversion nicht unterstuetzt ({Message})", error.Message);
        }

        throw new InvalidDataException(error?.Message ?? "Daemon-Handshake wurde abgewiesen.");
    }

    private static EffectiveDaemonConfiguration CreateConfiguration(ThinClientLaunchOptions options) =>
        new(
            options.MaxProjects ?? DaemonProtocol.DefaultMaxProjects,
            options.IdleExitMinutes ?? DaemonProtocol.DefaultIdleExitMinutes);

    private static void ReportPumpFailure(
        ILintConsole console,
        int attempt,
        ThinClientConnection connection,
        Exception? failure)
    {
        var message = failure?.Message ?? "unbekannter Pipe-Fehler";
        var suffix = attempt == 0 ? "; genau ein read-only Replay wird versucht" : "; kein weiterer Retry";
        console.WriteError($"[WARN]: Daemon-Pipe connectionId={connection.ConnectionId} PID={connection.ProcessId} abgebrochen ({message}){suffix}.");
    }

    private static bool IsEscapeEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("AINETLINTER_NO_DAEMON"), "1", StringComparison.Ordinal);

    private sealed record ConnectAttempt(ThinClientConnection? Connection, Exception? Failure);

    private sealed class ThinClientVersionConflictException(string message) : IOException(message);
}
