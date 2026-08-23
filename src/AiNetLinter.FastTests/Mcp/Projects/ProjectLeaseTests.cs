#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectLeaseTests
{
    [Fact]
    public void Dispose_DecrementsInFlightExactlyOnce_AndIgnoresDoubleDispose()
    {
        var factory = new TrackingServerFactory();
        using var server = factory.CreateServer(MinimalDefinition());
        var entry = new ProjectEntry("root", MinimalDefinition(), server, DateTime.UtcNow);

        var first = entry.OpenLease();
        Assert.Equal(1, entry.InFlightCount);
        first.Dispose();
        Assert.Equal(0, entry.InFlightCount);
        first.Dispose();
        Assert.Equal(0, entry.InFlightCount);

        var second = entry.OpenLease();
        var third = entry.OpenLease();
        Assert.Equal(2, entry.InFlightCount);
        second.Dispose();
        Assert.Equal(1, entry.InFlightCount);
        third.Dispose();
        Assert.Equal(0, entry.InFlightCount);
    }

    [Fact]
    public async Task InFlightCount_StaysAboveZero_UntilHeldUsageCompletes()
    {
        var factory = new TrackingServerFactory();
        using var server = factory.CreateServer(MinimalDefinition());
        var entry = new ProjectEntry("root", MinimalDefinition(), server, DateTime.UtcNow);
        var lease = entry.OpenLease();

        var releaseUsage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var usageCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            await releaseUsage.Task.WaitAsync(TimeSpan.FromSeconds(30));
            lease.Dispose();
            usageCompleted.TrySetResult();
        });

        Assert.Equal(1, entry.InFlightCount);

        releaseUsage.TrySetResult();
        await usageCompleted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, entry.InFlightCount);
    }

    private static ProjectDefinition MinimalDefinition() => new("app.slnx", "rules.json");
}
