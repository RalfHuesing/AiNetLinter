#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
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
    public void TryAcquire_OversizedRequestDoesNotEvictReleasedResources()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxDiskBytes: 10,
            MaxMemoryBytes: 10,
            MaxResidentResources: 2));
        var first = registry.TryAcquire(new ExternalResourceRequest("first", 5, 5));
        first.Lease!.Dispose();

        var oversized = registry.TryAcquire(new ExternalResourceRequest("oversized", 11, 1));
        var second = registry.TryAcquire(new ExternalResourceRequest("second", 5, 5));

        Assert.False(oversized.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, registry.ResidentCount);
        second.Lease!.Dispose();
    }

    [Fact]
    public void TryReserve_AccountsForConcurrentMaterializationAndRollsBack()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxDiskBytes: 10,
            MaxMemoryBytes: 10,
            MaxResidentResources: 2));

        Assert.True(registry.TryReserve(new ExternalResourceRequest("first", 8, 8), out var first, out _));
        Assert.False(registry.TryReserve(new ExternalResourceRequest("second", 3, 3), out var second, out var reason));
        Assert.Null(second);
        Assert.Contains("Diskbudget", reason, StringComparison.Ordinal);

        first!.Dispose();
        Assert.True(registry.TryReserve(new ExternalResourceRequest("second", 3, 3), out second, out _));
        second!.Dispose();
    }

    [Fact]
    public void TryReserve_PromotesToResidentLeaseExactlyOnce()
    {
        using var registry = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxResidentResources: 1));
        var request = new ExternalResourceRequest("foo", 3, 5);

        Assert.True(registry.TryReserve(request, out var reservation, out _));
        var lease = registry.PromoteReservation(reservation!);

        Assert.NotNull(lease);
        Assert.Equal(1, registry.ResidentCount);
        reservation!.Dispose();
        Assert.Equal(1, registry.ResidentCount);

        lease.Dispose();
    }

    [Fact]
    public async Task Dispose_DuringActiveOperationLeavesOperationLeaseSafeToRelease()
    {
        using var registry = new ExternalResourceRegistry();
        Assert.True(registry.TryBeginOperation(CancellationToken.None, out var operation));

        await Task.Run(registry.Dispose);

        operation!.Dispose();
        Assert.False(registry.TryBeginOperation(CancellationToken.None, out _));
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
