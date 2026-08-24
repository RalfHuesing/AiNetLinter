#nullable enable

using System.IO.Pipes;
using System.Text;
using AiNetLinter.Mcp.Daemon;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class DaemonPipeTransportContractTests
{
    [Fact]
    public void Endpoint_UsesVersionedCurrentUserNameAndCurrentUserOnlyPipeOption()
    {
        var endpoint = DaemonPipeEndpoint.ForUser("alice");

        Assert.Equal("ainetlinter.analyzer.v1.alice", endpoint.PipeName);
        Assert.Equal("alice", endpoint.UserName);
        Assert.True(endpoint.IsCurrentUserOnly);
        Assert.True(endpoint.Options.HasFlag(PipeOptions.Asynchronous));
    }

    [Fact]
    public void Transport_ResolvesInjectedUserForDeterministicEndpoint()
    {
        var transport = new DaemonPipeTransport(() => "test-user");

        Assert.Equal("ainetlinter.analyzer.v1.test-user", transport.Endpoint.PipeName);
        Assert.True(transport.Endpoint.IsCurrentUserOnly);
    }

    [Fact]
    public async Task InstanceLock_AllowsOneOwnerAndReleasesForNextOwner()
    {
        var pipeName = "daemon-lock-tests-" + Guid.NewGuid().ToString("N");
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = Task.Run(async () =>
        {
            using var first = new DaemonInstanceLock(pipeName);
            Assert.True(first.TryAcquire());
            acquired.SetResult();
            await release.Task;
        });

        await acquired.Task;
        try
        {
            using var second = new DaemonInstanceLock(pipeName);
            Assert.False(second.TryAcquire());
        }
        finally
        {
            release.SetResult();
            await owner;
        }

        using var third = new DaemonInstanceLock(pipeName);
        Assert.True(third.TryAcquire());
    }

    [Fact]
    public async Task Frame_RoundTripsOneJsonObjectPerLineWithoutChangingBytes()
    {
        var payload = Encoding.UTF8.GetBytes("{ \"jsonrpc\": \"2.0\", \"id\": 7, \"text\": \"Grüße\" }");
        using var output = new MemoryStream();
        await DaemonPipeTransport.WriteFrameAsync(output, payload, CancellationToken.None);

        var written = output.ToArray();
        Assert.Equal((byte)'\n', written[^1]);
        Assert.Equal(payload, written[..^1]);

        await using var connection = new DaemonPipeConnection(new MemoryStream(written));
        var received = await connection.ReadFrameAsync();

        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task Frame_RejectsNonObjectAndMultilineJsonDeterministically()
    {
        var arrayFrame = new MemoryStream(Encoding.UTF8.GetBytes("[]\n"));
        var multilineFrame = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":\n1}\n"));

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DaemonPipeTransport.ReadFrameAsync(arrayFrame, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await DaemonPipeTransport.ReadFrameAsync(multilineFrame, CancellationToken.None));
    }

    [Fact]
    public void SerializeFrame_ProducesSingleCompactJsonObject()
    {
        var payload = DaemonPipeTransport.SerializeFrame(new DaemonHello(
            "exe-1",
            9,
            EffectiveDaemonConfiguration.Default));
        var text = Encoding.UTF8.GetString(payload);

        Assert.DoesNotContain('\n', text);
        Assert.Contains("\"type\":\"hello\"", text, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disconnect_CancelsOnlyItsConnectionAndLeavesSecondConnectionUsable()
    {
        var blockingStream = new BlockingReadStream();
        await using var first = new DaemonPipeConnection(blockingStream);
        await using var second = new DaemonPipeConnection(new MemoryStream());
        var firstRead = first.ReadFrameAsync().AsTask();
        await blockingStream.ReadStarted;

        first.Disconnect();
        var exception = await Record.ExceptionAsync(async () => await firstRead);
        await second.WriteFrameAsync(Encoding.UTF8.GetBytes("{\"warm\":true}"));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.False(second.CancellationToken.IsCancellationRequested);
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task ReadStarted => readStarted.Task;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
