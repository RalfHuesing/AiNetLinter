#nullable enable

using System.IO.Pipelines;
using System.Text;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class ThinClientPumpContractTests
{
    [Fact]
    public async Task BytePump_KeepsReplayFrameWhenPipeBreaksWithoutAnswer()
    {
        using var session = PumpSession.StartIdle();
        await session.SendRequestFrameAsync("req-1");
        Assert.Equal("req-1", await session.ReadDaemonFrameAsync().ConfigureAwait(false));

        session.AbortDaemonSide();

        var result = await session.Result.ConfigureAwait(false);
        Assert.False(result.Completed);
        Assert.IsType<EndOfStreamException>(result.Failure);
        Assert.Equal("req-1", Encoding.UTF8.GetString(result.ReplayFrame ?? []));
    }

    [Fact]
    public async Task BytePump_ClearsReplayWindowOnceAnswerWasForwarded()
    {
        using var session = PumpSession.StartIdle();
        await session.SendRequestFrameAsync("req-1");
        Assert.Equal("req-1", await session.ReadDaemonFrameAsync().ConfigureAwait(false));
        await session.WriteDaemonResponseAsync("resp-1");
        Assert.Equal("resp-1", await session.ReadOutputFrameAsync().ConfigureAwait(false));

        session.AbortDaemonSide();

        var result = await session.Result.ConfigureAwait(false);
        Assert.False(result.Completed);
        Assert.IsType<EndOfStreamException>(result.Failure);
        Assert.Null(result.ReplayFrame);
    }

    [Fact]
    public async Task BytePump_WritesCapturedReplayFrameFirstOnRerun()
    {
        using var session = PumpSession.StartWithReplay(Encoding.UTF8.GetBytes("req-0"));

        Assert.Equal("req-0", await session.ReadDaemonFrameAsync().ConfigureAwait(false));
        await session.CompleteInputAsync().ConfigureAwait(false);

        var result = await session.Result.ConfigureAwait(false);
        Assert.True(result.Completed);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task BytePump_IdleTimeoutCarriesDistinguishableHangSignatureAndKeepsReplay()
    {
        using var session = PumpSession.StartIdle(idleTimeout: TimeSpan.FromMilliseconds(300));
        await session.SendRequestFrameAsync("req-hang");
        Assert.Equal("req-hang", await session.ReadDaemonFrameAsync().ConfigureAwait(false));

        var result = await session.Result.ConfigureAwait(false);

        Assert.False(result.Completed);
        var timeout = Assert.IsType<TimeoutException>(result.Failure);
        Assert.Contains("Hanger-Schutz-Zeitlimit", timeout.Message, StringComparison.Ordinal);
        Assert.Equal("req-hang", Encoding.UTF8.GetString(result.ReplayFrame ?? []));
    }

    [Fact]
    public async Task BytePump_IdleTimeoutWithoutAnyFrameStillYieldsTimeoutSignature()
    {
        using var session = PumpSession.StartIdle(idleTimeout: TimeSpan.FromMilliseconds(200));

        var result = await session.Result.ConfigureAwait(false);

        Assert.False(result.Completed);
        Assert.IsType<TimeoutException>(result.Failure);
        Assert.Null(result.ReplayFrame);
    }

    [Fact]
    public async Task BytePump_CallerCancellationStaysUnattributedInsteadOfTimeout()
    {
        using var source = new CancellationTokenSource();
        using var session = PumpSession.StartIdle(externalToken: source.Token);
        source.Cancel();

        var result = await session.Result.ConfigureAwait(false);

        // Die Abbruchentscheidung trifft der Aufrufer — die Haenger-Signatur
        // darf bei eigenem Abbruch nicht erscheinen.
        Assert.Null(result.Failure);
        Assert.Null(result.ReplayFrame);
    }

    private sealed class PumpSession : IDisposable
    {
        private readonly Pipe input = new();
        private readonly Pipe output = new();
        private readonly Stream clientSide;
        private readonly Stream daemonSide;
        private readonly Task<DaemonPumpResult> run;

        private PumpSession(
            TimeSpan idleTimeout,
            byte[]? replayFrame,
            CancellationToken externalToken)
        {
            (clientSide, daemonSide) = ThinClientPipeTestDoubles.CreateDuplexPair();
            run = DaemonBytePump.RunAsync(
                input.Reader.AsStream(),
                output.Writer.AsStream(),
                clientSide,
                new DaemonPumpOptions(idleTimeout, replayFrame),
                externalToken);
        }

        public static PumpSession StartIdle(
            TimeSpan? idleTimeout = null,
            CancellationToken externalToken = default) =>
            new(idleTimeout ?? TimeSpan.FromSeconds(10), null, externalToken);

        public static PumpSession StartWithReplay(byte[] replayFrame) =>
            new(TimeSpan.FromSeconds(10), replayFrame, CancellationToken.None);

        public Stream DaemonSide => daemonSide;

        public Task<DaemonPumpResult> Result => run;

        public async Task SendRequestFrameAsync(string payload) =>
            await input.Writer.WriteAsync(Encoding.UTF8.GetBytes(payload + "\n")).ConfigureAwait(false);

        public async Task CompleteInputAsync() =>
            await input.Writer.CompleteAsync().ConfigureAwait(false);

        public Task<string> ReadDaemonFrameAsync() =>
            ThinClientPipeTestDoubles.ReadFrameAsync(daemonSide);

        public async Task WriteDaemonResponseAsync(string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload + "\n");
            await daemonSide.WriteAsync(bytes).ConfigureAwait(false);
            await daemonSide.FlushAsync().ConfigureAwait(false);
        }

        public async Task<string> ReadOutputFrameAsync()
        {
            using var stream = output.Reader.AsStream();
            return await ThinClientPipeTestDoubles.ReadFrameAsync(stream).ConfigureAwait(false);
        }

        public void AbortDaemonSide() => daemonSide.Dispose();

        public void Dispose()
        {
            _ = input.Writer.CompleteAsync();
            _ = input.Reader.CompleteAsync();
            _ = output.Writer.CompleteAsync();
            _ = output.Reader.CompleteAsync();
            clientSide.Dispose();
            daemonSide.Dispose();
        }
    }
}
