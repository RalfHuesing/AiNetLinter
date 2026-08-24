#nullable enable

using System.Diagnostics;
using System.Text.Json;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Mcp.Lifetime;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Daemon;

internal static class ThinClientProxy
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PumpIdleTimeout = TimeSpan.FromMinutes(2);
    private const int MaximumRetries = 1;

    internal static async Task<int> RunAsync(
        LinterArgs args,
        CancellationToken cancellationToken = default,
        ILintConsole? console = null)
    {
        var clientConsole = console ?? LinterConsole.Instance;
        if (IsEscapeEnabled())
        {
            return await McpServerCommand.RunAsync(args, cancellationToken, clientConsole).ConfigureAwait(false);
        }

        var validationError = args.Validate();
        if (validationError is not null)
        {
            clientConsole.WriteError(validationError);
            return 1;
        }

        await using var lifetime = McpServerLifetime.Start(args.ParentPid, cancellationToken, clientConsole.WriteError);
        var launchOptions = new ThinClientLaunchOptions(
            args.McpProjectTtlMinutes,
            args.McpMaxProjects,
            args.McpDaemonIdleExitMinutes,
            args.McpLogPath);
        var replayFrame = (byte[]?)null;
        try
        {
            for (var attempt = 0; attempt <= MaximumRetries; attempt++)
            {
                var connection = await ConnectOrStartAsync(launchOptions, lifetime.Token, clientConsole).ConfigureAwait(false);
                try
                {
                    var pumpResult = await DaemonBytePump.RunAsync(
                        Console.OpenStandardInput(),
                        Console.OpenStandardOutput(),
                        connection.Pipe.Stream,
                        new DaemonPumpOptions(PumpIdleTimeout, replayFrame),
                        lifetime.Token).ConfigureAwait(false);
                    if (pumpResult.Completed) return 0;

                    ReportPumpFailure(clientConsole, attempt, connection, pumpResult.Failure);
                    replayFrame = pumpResult.ReplayFrame;
                    if (attempt == MaximumRetries)
                    {
                        return 2;
                    }

                    TerminateIdentifiedDaemon(connection.ProcessId, clientConsole);
                }
                finally
                {
                    await connection.Pipe.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.Token.IsCancellationRequested)
        {
            return 0;
        }

        return 2;
    }

    private static async Task<ThinClientConnection> ConnectOrStartAsync(
        ThinClientLaunchOptions options,
        CancellationToken cancellationToken,
        ILintConsole console)
    {
        var firstAttempt = await TryConnectFirstAsync(options, cancellationToken, console).ConfigureAwait(false);
        if (firstAttempt.Connection is not null) return firstAttempt.Connection;
        if (!ThinClientLauncher.TryStartDetached(options, console.WriteError))
        {
            throw firstAttempt.Failure ?? new IOException("Der Daemon konnte nicht gestartet werden.");
        }

        return await WaitForReadinessAsync(options, cancellationToken, console).ConfigureAwait(false);
    }

    private static async Task<ConnectAttempt> TryConnectFirstAsync(
        ThinClientLaunchOptions options,
        CancellationToken cancellationToken,
        ILintConsole console)
    {
        try
        {
            var connection = await ConnectAsync(options, ConnectTimeout, cancellationToken, console).ConfigureAwait(false);
            return new ConnectAttempt(connection, null);
        }
        catch (ThinClientVersionConflictException)
        {
            throw;
        }
        catch (Exception exception)
        {
            console.WriteError($"[INFO]: Daemon-Connect-first fehlgeschlagen: {exception.Message}");
            return new ConnectAttempt(null, exception);
        }
    }

    private static async Task<ThinClientConnection> WaitForReadinessAsync(
        ThinClientLaunchOptions options,
        CancellationToken cancellationToken,
        ILintConsole console)
    {
        using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readiness.CancelAfter(ReadinessTimeout);
        while (true)
        {
            try
            {
                return await ConnectAsync(options, ConnectTimeout, readiness.Token, console).ConfigureAwait(false);
            }
            catch (ThinClientVersionConflictException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await DelayReadinessRetryAsync(readiness, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                await DelayReadinessRetryAsync(readiness, cancellationToken).ConfigureAwait(false);
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
        ILintConsole console)
    {
        using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connect.CancelAfter(timeout);
        var transport = new DaemonPipeTransport();
        var pipe = await transport.ConnectAsync(connect.Token).ConfigureAwait(false);
        try
        {
            var hello = new DaemonHello(
                McpServerOptionsFactory.GetServerVersion(),
                Environment.ProcessId,
                CreateConfiguration(options));
            await pipe.WriteJsonFrameAsync(hello, connect.Token).ConfigureAwait(false);
            var response = await pipe.ReadFrameAsync(connect.Token).ConfigureAwait(false)
                ?? throw new EndOfStreamException("Daemon beendete den Handshake ohne Antwort.");
            return ReadHandshakeResponse(pipe, response, CreateConfiguration(options), console);
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
            throw new ThinClientVersionConflictException(error.Message);
        }

        throw new InvalidDataException(error?.Message ?? "Daemon-Handshake wurde abgewiesen.");
    }

    private static EffectiveDaemonConfiguration CreateConfiguration(ThinClientLaunchOptions options) =>
        new(
            options.MaxProjects ?? DaemonProtocol.DefaultMaxProjects,
            options.IdleExitMinutes ?? DaemonProtocol.DefaultIdleExitMinutes,
            options.LogPath ?? DaemonProtocol.DefaultLogTarget);

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

    private static void TerminateIdentifiedDaemon(int processId, ILintConsole console)
    {
        if (processId <= 0 || processId == Environment.ProcessId) return;
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited) process.Kill();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            console.WriteError($"[WARN]: Identifizierter Daemon PID={processId} konnte nicht beendet werden: {exception.Message}");
        }
    }

    private static bool IsEscapeEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("AINETLINTER_NO_DAEMON"), "1", StringComparison.Ordinal);

    private sealed record ConnectAttempt(ThinClientConnection? Connection, Exception? Failure);

    private sealed record ThinClientConnection(DaemonPipeConnection Pipe, int ProcessId, int ConnectionId);

    private sealed class ThinClientVersionConflictException(string message) : IOException(message);
}
