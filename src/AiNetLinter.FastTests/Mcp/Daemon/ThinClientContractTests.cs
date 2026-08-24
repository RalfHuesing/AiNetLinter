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
        var (clientSide, daemonSide) = ThinClientPipeTestDoubles.CreateDuplexPair();
        await using var _ = clientSide;
        var run = DaemonBytePump.RunAsync(
            input.Reader.AsStream(),
            output.Writer.AsStream(),
            clientSide,
            new DaemonPumpOptions(TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes("opaque-not-json\n"));
        Assert.Equal("opaque-not-json", await ThinClientPipeTestDoubles.ReadFrameAsync(daemonSide));
        await daemonSide.WriteAsync(Encoding.UTF8.GetBytes("opaque-response\n"));
        Assert.Equal("opaque-response", await ThinClientPipeTestDoubles.ReadFrameAsync(output.Reader.AsStream()));
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
}
