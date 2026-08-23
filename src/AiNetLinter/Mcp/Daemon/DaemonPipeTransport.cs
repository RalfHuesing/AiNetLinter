#nullable enable

using System.IO.Pipes;
using System.Text.Json;

namespace AiNetLinter.Mcp.Daemon;

internal sealed record DaemonPipeEndpoint(
    string PipeName,
    string UserName,
    PipeOptions Options)
{
    internal bool IsCurrentUserOnly => Options.HasFlag(PipeOptions.CurrentUserOnly);

    internal static DaemonPipeEndpoint ForUser(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        return new DaemonPipeEndpoint(
            DaemonProtocol.GetPipeName(userName),
            userName,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    internal static DaemonPipeEndpoint ForCurrentUser() =>
        ForUser(DaemonProtocol.CurrentUserName);
}

internal sealed class DaemonPipeTransport
{
    internal DaemonPipeTransport(Func<string>? userNameProvider = null)
    {
        var resolveUserName = userNameProvider ?? (() => DaemonProtocol.CurrentUserName);
        Endpoint = DaemonPipeEndpoint.ForUser(resolveUserName());
    }

    internal DaemonPipeEndpoint Endpoint { get; }

    internal NamedPipeServerStream CreateServerStream() => new(
        Endpoint.PipeName,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        Endpoint.Options);

    internal NamedPipeClientStream CreateClientStream() => new(
        ".",
        Endpoint.PipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);

    internal DaemonPipeConnection CreateConnection(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new DaemonPipeConnection(stream);
    }

    internal async ValueTask<DaemonPipeConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        var server = CreateServerStream();
        try
        {
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            return CreateConnection(server);
        }
        catch
        {
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async ValueTask<DaemonPipeConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var client = CreateClientStream();
        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return CreateConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    internal static async ValueTask WriteFrameAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ValidateFrame(payload.Span);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<byte[]?> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var singleByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(singleByte, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return CompleteFrameAtEnd(buffer);
            }

            if (singleByte[0] == (byte)'\n')
            {
                return CompleteFrame(buffer);
            }

            buffer.WriteByte(singleByte[0]);
        }
    }

    internal static byte[] SerializeFrame<T>(T message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, DaemonProtocol.JsonOptions);
        ValidateFrame(payload);
        return payload;
    }

    private static byte[]? CompleteFrameAtEnd(MemoryStream buffer)
    {
        if (buffer.Length == 0)
        {
            return null;
        }

        throw new InvalidDataException("Der JSON-Frame endet ohne Newline-Trennzeichen.");
    }

    private static byte[] CompleteFrame(MemoryStream buffer)
    {
        var payload = buffer.ToArray();
        if (payload is [.., (byte)'\r'])
        {
            payload = payload[..^1];
        }

        ValidateFrame(payload);
        return payload;
    }

    private static void ValidateFrame(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty || payload.Contains((byte)'\r') || payload.Contains((byte)'\n'))
        {
            throw new InvalidDataException("Ein JSON-Frame muss genau eine nichtleere Zeile sein.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Ein JSON-Frame muss ein Objekt sein.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Das JSON-Frame ist ungueltig.", exception);
        }
    }
}

internal sealed class DaemonPipeConnection : IAsyncDisposable
{
    private readonly Stream stream;
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly object lifecycleGate = new();
    private bool disposed;

    internal DaemonPipeConnection(Stream stream)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    internal CancellationToken CancellationToken => cancellation.Token;

    internal void Disconnect()
    {
        lock (lifecycleGate)
        {
            if (!disposed)
            {
                cancellation.Cancel();
            }
        }
    }

    internal async ValueTask<byte[]?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken, cancellationToken);
        return await DaemonPipeTransport.ReadFrameAsync(stream, linked.Token).ConfigureAwait(false);
    }

    internal async ValueTask<T?> ReadJsonFrameAsync<T>(CancellationToken cancellationToken = default)
    {
        var payload = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        return payload is null
            ? default
            : JsonSerializer.Deserialize<T>(payload, DaemonProtocol.JsonOptions);
    }

    internal async ValueTask WriteFrameAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken, cancellationToken);
        var entered = false;
        try
        {
            await writeGate.WaitAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            await DaemonPipeTransport.WriteFrameAsync(stream, payload, linked.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            if (entered)
            {
                writeGate.Release();
            }
        }
    }

    internal ValueTask WriteJsonFrameAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        var payload = DaemonPipeTransport.SerializeFrame(message);
        return WriteFrameAsync(payload, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation.Cancel();
        }

        await stream.DisposeAsync().ConfigureAwait(false);
        writeGate.Dispose();
        cancellation.Dispose();
    }

    private void ThrowIfDisposed()
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
