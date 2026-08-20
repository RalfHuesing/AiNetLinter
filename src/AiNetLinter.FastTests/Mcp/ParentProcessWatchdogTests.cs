#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Lifetime;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

// @covers ParentProcessWatchdog
[Trait("Category", "Unit")]
public sealed class ParentProcessWatchdogTests
{
    [Fact]
    public async Task MissingParentProcess_CancelsShutdownSource()
    {
        using var shutdownSource = new CancellationTokenSource();
        await using var watchdog = ParentProcessWatchdog.Start(
            int.MaxValue,
            shutdownSource,
            TimeSpan.FromMilliseconds(10));

        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, shutdownSource.Token);
        var completedTask = await Task.WhenAny(cancellationTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(cancellationTask, completedTask);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotCancelShutdownSource()
    {
        using var shutdownSource = new CancellationTokenSource();
        await using (var watchdog = ParentProcessWatchdog.Start(
                         Environment.ProcessId,
                         shutdownSource,
                         TimeSpan.FromMilliseconds(10)))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        Assert.False(shutdownSource.IsCancellationRequested);
    }
}
