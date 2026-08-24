#nullable enable

using System.Diagnostics;
using AiNetLinter.FastTests.Mcp.Projects;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class DaemonWarmupTests
{
    [Fact]
    public async Task Warmup_IsBoundedAndDoesNotBlockInteractiveLease()
    {
        using var temp = TestTempDirectory.Create("daemon-warmup-");
        var roots = Enumerable.Range(1, 4)
            .Select(index => ProjectRegistryFixture.CreateProjectRoot(temp, $"project-{index}"))
            .ToArray();
        var factory = new TrackingServerFactory();
        var registry = ProjectRegistryFixture.Create(factory.Factory);
        await using var mru = new MruStateStore(new MruStateStoreOptions(temp.GetPath("state.json"), TimeProvider.System));
        await using var host = new DaemonHost(new DaemonHostOptions(
            new DaemonRegistryAdapter(registry),
            mru,
            new DaemonPipeTransport(() => "daemon-warmup-tests"),
            TimeProvider.System,
            TimeSpan.FromMinutes(10),
            new EffectiveDaemonConfiguration(4, 10m, "stderr"),
            LinterConsole.Instance,
            _ => Task.CompletedTask));
        using var cancellation = new CancellationTokenSource();

        var warmup = host.WarmupForTestAsync(
            roots.Select(root => new MruStateEntry(root, DateTime.UtcNow)).ToArray(),
            cancellation.Token);
        await WaitUntilAsync(() => factory.LoadsStarted >= 2);

        var started = Stopwatch.GetTimestamp();
        var interactive = registry.Lease(roots[2]);
        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.True(interactive.Succeeded);
        Assert.True(elapsed < TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => factory.LoadsStarted >= 3);
        Assert.True(factory.LoadsStarted >= 3);
        interactive.Lease!.Dispose();

        cancellation.Cancel();
        await warmup;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition()) return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "Die Warmup-Loads wurden nicht innerhalb des Testfensters gestartet.");
    }
}
