#nullable enable

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.TestKit;

// Gekreuztes In-Memory-Duplexpaar fuer Pump- und Connect-or-Start-Contracts:
// was der Client schreibt, liest die Daemon-Seite und umgekehrt — byte-opak.
internal static class ThinClientPipeTestDoubles
{
    internal static (Stream ClientSide, Stream DaemonSide) CreateDuplexPair()
    {
        var toDaemon = new Pipe();
        var fromDaemon = new Pipe();
        var clientSide = new DuplexStream(fromDaemon.Reader.AsStream(), toDaemon.Writer.AsStream());
        var daemonSide = new DuplexStream(toDaemon.Reader.AsStream(), fromDaemon.Writer.AsStream());
        return (clientSide, daemonSide);
    }

    internal static async Task<string> ReadFrameAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        var singleByte = new byte[1];
        while (await stream.ReadAsync(singleByte).ConfigureAwait(false) != 0)
        {
            if (singleByte[0] == (byte)'\n') return Encoding.UTF8.GetString(buffer.ToArray());
            buffer.WriteByte(singleByte[0]);
        }

        throw new EndOfStreamException();
    }
}

// Duplex-Adapter ueber zwei gekreuzte Pipes: Lesen und Schreiben laufen auf
// getrennten Richtungen, damit Client- und Daemon-Seite denselben Vertrag
// wie ein Named-Pipe-Bytestrom erfuellen.
internal sealed class DuplexStream(Stream reader, Stream writer) : Stream
{
    public override bool CanRead => reader.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => writer.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => writer.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => writer.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => reader.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        reader.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => writer.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        writer.WriteAsync(buffer, cancellationToken);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            reader.Dispose();
            writer.Dispose();
        }

        base.Dispose(disposing);
    }
}

// Nachgebauter Daemonepunkt fuer die Client-Seite: die ersten Verbindungsversuche
// scheitern kontrolliert, danach liefert jede Verbindung ein Duplexpaar, dessen
// Daemon-Seite vom serve-Callback bedient wird.
internal sealed class ScriptedMockPipeTransport
{
    private readonly int initialConnectFailures;
    private readonly Func<bool>? acceptWhen;
    private readonly Func<DaemonPipeConnection, int, Task> serveConnection;
    private int connectAttempts;
    private int servedConnections;

    public ScriptedMockPipeTransport(
        int initialConnectFailures,
        Func<DaemonPipeConnection, int, Task>? serveConnection = null,
        Func<bool>? acceptWhen = null)
    {
        this.initialConnectFailures = initialConnectFailures;
        this.acceptWhen = acceptWhen;
        this.serveConnection =
            serveConnection ?? ((connection, index) => MockDaemonScript.WelcomeThenHoldAsync(connection, 4711, index));
    }

    public int ConnectAttempts => Volatile.Read(ref connectAttempts);

    public async ValueTask<DaemonPipeConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref connectAttempts);
        if (attempt <= initialConnectFailures || acceptWhen is { } condition && !condition())
        {
            throw new IOException($"Mock-Daemonepunkt ist noch nicht bereit (Versuch {attempt}).");
        }

        var (clientSide, daemonSide) = ThinClientPipeTestDoubles.CreateDuplexPair();
        var connectionIndex = Interlocked.Increment(ref servedConnections);
        _ = Task.Run(() => ServeAsync(daemonSide, connectionIndex));
        return new DaemonPipeConnection(clientSide);
    }

    private async Task ServeAsync(Stream daemonSide, int connectionIndex)
    {
        try
        {
            await serveConnection(new DaemonPipeConnection(daemonSide), connectionIndex).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Client-seitiger Abbruch waehrend des Skripts ist Teil der Szenarien.
        }
    }
}

// Skriptbausteine fuer die Mock-Daemon-Seite eines Duplexpaares.
internal static class MockDaemonScript
{
    public static Task WelcomeThenHoldAsync(DaemonPipeConnection connection, int processId, int connectionId) =>
        WelcomeThenAsync(connection, processId, connectionId, _ => Task.Delay(Timeout.InfiniteTimeSpan));

    public static async Task WelcomeThenAbortAsync(DaemonPipeConnection connection, int processId, int connectionId)
    {
        await WriteWelcomeAsync(connection, processId, connectionId).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public static async Task WelcomeCaptureNextFrameThenAbortAsync(
        DaemonPipeConnection connection,
        int processId,
        int connectionId,
        ConcurrentQueue<byte[]> capturedFrames)
    {
        _ = await connection.ReadJsonFrameAsync<DaemonHello>().ConfigureAwait(false);
        await WriteWelcomeAsync(connection, processId, connectionId).ConfigureAwait(false);
        var frame = await connection.ReadFrameAsync().ConfigureAwait(false);
        if (frame is not null) capturedFrames.Enqueue(frame);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public static async Task WelcomeThenAsync(
        DaemonPipeConnection connection,
        int processId,
        int connectionId,
        Func<DaemonPipeConnection, Task> afterWelcome)
    {
        await WriteWelcomeAsync(connection, processId, connectionId).ConfigureAwait(false);
        await afterWelcome(connection).ConfigureAwait(false);
    }

    public static async Task WriteWelcomeAsync(DaemonPipeConnection connection, int processId, int connectionId) =>
        await connection.WriteJsonFrameAsync(new DaemonWelcome(
            "9.9.mock",
            McpServerOptionsFactory.GetServerVersion(),
            processId,
            EffectiveDaemonConfiguration.Default)
        {
            ConnectionId = connectionId,
        }).ConfigureAwait(false);
}
