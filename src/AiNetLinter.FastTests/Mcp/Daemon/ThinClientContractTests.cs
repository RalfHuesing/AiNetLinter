#nullable enable

using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using AiNetLinter.Mcp.Daemon;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class ThinClientContractTests
{
    [Fact]
    public void Launcher_ForwardsDaemonFlagsWithoutOwningStdoutOrStderr()
    {
        var startInfo = ThinClientLauncher.CreateStartInfo(new ThinClientLaunchOptions(3.5m, 2, 0.25m, "stderr"));

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.Contains("--daemon-start", startInfo.ArgumentList);
        Assert.Contains("--mcp-project-ttl-minutes", startInfo.ArgumentList);
        Assert.Contains("--mcp-max-projects", startInfo.ArgumentList);
        Assert.Contains("--mcp-daemon-idle-exit-minutes", startInfo.ArgumentList);
        Assert.Contains("--mcp-log", startInfo.ArgumentList);
    }

    [Fact]
    public async Task BytePump_ForwardsOpaqueFramesWithoutJsonInterpretation()
    {
        var input = new Pipe();
        var output = new Pipe();
        var toDaemon = new Pipe();
        var fromDaemon = new Pipe();
        await using var daemonPipe = new DuplexStream(fromDaemon.Reader.AsStream(), toDaemon.Writer.AsStream());
        var run = DaemonBytePump.RunAsync(
            input.Reader.AsStream(),
            output.Writer.AsStream(),
            daemonPipe,
            new DaemonPumpOptions(TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes("opaque-not-json\n"));
        Assert.Equal("opaque-not-json", await ReadFrameAsync(toDaemon.Reader.AsStream()));
        await fromDaemon.Writer.WriteAsync(Encoding.UTF8.GetBytes("opaque-response\n"));
        Assert.Equal("opaque-response", await ReadFrameAsync(output.Reader.AsStream()));
        await input.Writer.CompleteAsync();

        Assert.True((await run).Completed);
    }

    [Fact]
    public void Welcome_RoundTripsConnectionId()
    {
        var welcome = new DaemonWelcome("1.0.1", "1.0.1", 42, EffectiveDaemonConfiguration.Default)
        {
            ConnectionId = 7,
        };

        var json = JsonSerializer.Serialize(welcome, DaemonProtocol.JsonOptions);
        var restored = JsonSerializer.Deserialize<DaemonWelcome>(json, DaemonProtocol.JsonOptions);

        Assert.Equal(7, restored?.ConnectionId);
    }

    private static async Task<string> ReadFrameAsync(Stream stream)
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

    private sealed class DuplexStream(Stream reader, Stream writer) : Stream
    {
        public override bool CanRead => reader.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => writer.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => writer.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => writer.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => reader.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => reader.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => writer.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => writer.WriteAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing) { if (disposing) { reader.Dispose(); writer.Dispose(); } base.Dispose(disposing); }
    }
}
