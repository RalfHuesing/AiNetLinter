#nullable enable

using System;
using System.Threading;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers ExternalResourceRegistry
public sealed class ExternalResourceRegistryTests
{
    [Fact]
    public void TryAcquire_DeduplicatesIdentityAndTracksIndependentBudgets()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxDiskBytes: 100,
            MaxMemoryBytes: 100,
            MaxResidentResources: 2));

        var first = registry.TryAcquire(new ExternalResourceRequest("foo", 40, 20));
        var alias = registry.TryAcquire(new ExternalResourceRequest("foo", 999, 999));

        Assert.True(first.Succeeded);
        Assert.True(alias.Succeeded);
        Assert.Equal(1, registry.ResidentCount);
        Assert.Equal(40, alias.Health.DiskBytes);
        Assert.Equal(20, alias.Health.MemoryBytes);

        first.Lease!.Dispose();
        alias.Lease!.Dispose();
    }

    [Fact]
    public void TryAcquire_ReportsCapacityWithoutEvictingActiveResource()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(MaxResidentResources: 1));
        var first = registry.TryAcquire(new ExternalResourceRequest("foo", 1, 1));
        var second = registry.TryAcquire(new ExternalResourceRequest("bar", 1, 1));

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(ExternalResourceHealth.CapacityExceeded, second.Health.Health);
        Assert.Equal(1, registry.ResidentCount);
        Assert.Contains("Ressourcenlimit", second.FailureReason, StringComparison.Ordinal);

        first.Lease!.Dispose();
    }

    [Fact]
    public void EvictIdle_RemovesOnlyReleasedResourcesAndMakesCapacityAvailable()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxResidentResources: 1,
            IdleTtl: TimeSpan.FromMinutes(1),
            Clock: clock));
        var first = registry.TryAcquire(new ExternalResourceRequest("foo", 1, 1));
        first.Lease!.Dispose();

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, registry.EvictIdle());
        var second = registry.TryAcquire(new ExternalResourceRequest("bar", 1, 1));

        Assert.True(second.Succeeded);
        second.Lease!.Dispose();
    }

    [Fact]
    public void TryBeginOperation_ExposesParallelCapacityAndHonorsCancellation()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(MaxParallelOperations: 1));
        Assert.True(registry.TryBeginOperation(CancellationToken.None, out var first));
        Assert.False(registry.TryBeginOperation(CancellationToken.None, out var second));
        Assert.Equal(ExternalResourceHealth.Degraded, registry.Health.Health);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => registry.TryBeginOperation(cancellation.Token, out _));

        first!.Dispose();
        Assert.True(registry.TryBeginOperation(CancellationToken.None, out second));
        second!.Dispose();
    }

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset utcNow = initial;

        internal void Advance(TimeSpan value) => utcNow += value;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
