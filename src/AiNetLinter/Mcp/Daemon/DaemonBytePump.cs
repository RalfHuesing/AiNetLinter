#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonPumpOptions(TimeSpan IdleTimeout, byte[]? ReplayFrame = null);

internal sealed record DaemonPumpResult(
    bool Completed,
    byte[]? ReplayFrame,
    Exception? Failure);

internal static class DaemonBytePump
{
    private const int MaxFrameBytes = 16 * 1024 * 1024;

    internal static async Task<DaemonPumpResult> RunAsync(
        Stream standardInput,
        Stream standardOutput,
        Stream pipe,
        DaemonPumpOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(options);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.IdleTimeout > TimeSpan.Zero)
        {
            linked.CancelAfter(options.IdleTimeout);
        }

        var replay = new ReplayWindow(options.ReplayFrame);
        var inputTask = PumpInputAsync(standardInput, pipe, replay, options.ReplayFrame, linked.Token);
        var outputTask = PumpOutputAsync(pipe, standardOutput, replay, linked.Token);
        var completed = await Task.WhenAny(inputTask, outputTask).ConfigureAwait(false);
        linked.Cancel();
        if (completed == inputTask && inputTask.Status == TaskStatus.RanToCompletion)
        {
            // stdin-EOF beendet die Thin-Client-Session. Die Pipe wird geschlossen,
            // damit ein Daemon, der die Server-Seite offen haelt, keinen Hang erzeugt.
            await pipe.DisposeAsync().ConfigureAwait(false);
        }

        var inputFailure = await ObserveAsync(inputTask).ConfigureAwait(false);
        var outputFailure = await ObserveAsync(outputTask).ConfigureAwait(false);

        var failure = ReadFailure(
            inputTask,
            outputTask,
            inputFailure,
            outputFailure,
            completed,
            linked.IsCancellationRequested,
            cancellationToken);
        if (completed == inputTask && failure is null)
        {
            return new DaemonPumpResult(true, null, null);
        }

        return new DaemonPumpResult(false, replay.Take(), failure);
    }

    private static async Task PumpInputAsync(
        Stream input,
        Stream pipe,
        ReplayWindow replay,
        byte[]? initialReplayFrame,
        CancellationToken cancellationToken)
    {
        if (initialReplayFrame is not null)
        {
            await WriteFrameAsync(pipe, initialReplayFrame, cancellationToken).ConfigureAwait(false);
        }

        while (true)
        {
            var frame = await ReadFrameAsync(input, cancellationToken).ConfigureAwait(false);
            if (frame is null) return;

            replay.Set(frame);
            await WriteFrameAsync(pipe, frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PumpOutputAsync(
        Stream pipe,
        Stream output,
        ReplayWindow replay,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false)
                ?? throw new EndOfStreamException("Die Daemon-Pipe wurde ohne Antwort beendet.");
            await WriteFrameAsync(output, frame, cancellationToken).ConfigureAwait(false);
            replay.Clear();
        }
    }

    private static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var singleByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(singleByte, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (buffer.Length == 0) return null;
                throw new EndOfStreamException("Ein Rohframe endet ohne Newline.");
            }

            if (singleByte[0] == (byte)'\n') return buffer.ToArray();
            if (buffer.Length >= MaxFrameBytes)
            {
                throw new InvalidDataException("Ein Rohframe ueberschreitet das Transportlimit.");
            }

            buffer.WriteByte(singleByte[0]);
        }
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Exception? ReadFailure(
        Task inputTask,
        Task outputTask,
        Exception? inputFailure,
        Exception? outputFailure,
        Task completedTask,
        bool pumpCancelled,
        CancellationToken callerToken)
    {
        if (callerToken.IsCancellationRequested) return null;
        if (completedTask == inputTask && inputTask.Status == TaskStatus.RanToCompletion) return null;
        if (pumpCancelled && inputTask.IsCanceled && outputTask.IsCanceled) return null;
        return inputFailure ??
               outputFailure ??
               (completedTask == outputTask && outputTask.IsCanceled
                   ? new TimeoutException("Die Daemon-Pipe antwortete nicht innerhalb des Hanger-Schutz-Zeitlimits.")
                   : null);
    }

    private static async Task<Exception?> ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class ReplayWindow
    {
        private readonly object gate = new();
        private byte[]? frame;

        internal ReplayWindow(byte[]? initialFrame) => frame = initialFrame;

        internal void Set(byte[] value)
        {
            lock (gate) frame = value;
        }

        internal void Clear()
        {
            lock (gate) frame = null;
        }

        internal byte[]? Take()
        {
            lock (gate) return frame;
        }
    }
}
