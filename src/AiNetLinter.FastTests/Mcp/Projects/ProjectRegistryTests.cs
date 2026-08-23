#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Projects;

[Trait("Category", "Unit")]
public sealed class ProjectRegistryTests
{
    [Fact]
    public async Task Lease_NormalizesRootSpellings_ToSingleResidentEntry()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-keys-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory();
        await using var registry = CreateRegistry(factory, new FakeClock());

        var first = registry.Lease(root);
        using var firstLease = first.Lease;
        var trailingSeparator = registry.Lease(root + Path.DirectorySeparatorChar);
        using var trailingLease = trailingSeparator.Lease;
        var uppercase = registry.Lease(root.ToUpperInvariant());
        using var uppercaseLease = uppercase.Lease;
        var forwardSlashes = registry.Lease(root.Replace('\\', '/'));
        using var forwardLease = forwardSlashes.Lease;

        Assert.True(first.Succeeded);
        Assert.Equal(1, factory.InstancesCreated);
        Assert.Same(firstLease!.Server, trailingLease!.Server);
        Assert.Same(firstLease!.Server, uppercaseLease!.Server);
        Assert.Same(firstLease!.Server, forwardLease!.Server);
    }

    [Fact]
    public async Task Lease_HitTouchesLastUsedUtc_AndSurvivesTotalAgeBeyondTtl()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-touch-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, idleTtlMinutes: 15);

        var initial = registry.Lease(root);
        var serverInitial = initial.Lease!.Server;
        initial.Lease!.Dispose();
        clock.AdvanceMinutes(14);
        await registry.RunEvictionTickAsync();
        Assert.Equal(0, factory.LoadsCancelled);
        Assert.Equal(1, factory.InstancesCreated);

        var touched = registry.Lease(root);
        var serverTouched = touched.Lease!.Server;
        touched.Lease!.Dispose();
        clock.AdvanceMinutes(16);
        await registry.RunEvictionTickAsync();

        var reloaded = registry.Lease(root);
        using var reloadedLease = reloaded.Lease;

        Assert.Same(serverInitial, serverTouched);
        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(serverInitial, reloadedLease!.Server);
    }

    [Fact]
    public async Task Lease_MissingDefinitionFile_ReturnsLoaderErrorWithoutResidentEntry()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-uninit-");
        var root = Path.Combine(tempDir.DirectoryPath, "proj");
        var factory = new TrackingServerFactory();
        await using var registry = CreateRegistry(factory, new FakeClock());

        var failed = registry.Lease(root);

        Assert.False(failed.Succeeded);
        Assert.Null(failed.Lease);
        Assert.Equal(ProjectErrorCodes.ProjectNotInitialized, failed.ErrorCode);
        Assert.Equal(0, factory.InstancesCreated);

        CreateProjectRoot(tempDir, "proj");
        var retry = registry.Lease(root);
        using var retryLease = retry.Lease;

        Assert.True(retry.Succeeded);
        Assert.Equal(1, factory.InstancesCreated);
    }

    [Fact]
    public async Task Lease_ParallelCallersOnSameRoot_CreateExactlyOneInstance()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-dedupe-");
        var root = CreateProjectRoot(tempDir, "proj");
        var clock = new FakeClock();
        var factory = new TrackingServerFactory();
        var factoryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseFactory = new ManualResetEventSlim(false);
        await using var registry = new ProjectRegistry(new ProjectRegistryOptions(
            definition =>
            {
                factoryEntered.TrySetResult();
                releaseFactory.Wait(TimeSpan.FromSeconds(30));
                return ProjectInstanceCreation.Resident(factory.CreateServer(definition));
            },
            clock));

        var firstCall = Task.Run(() => registry.Lease(root));
        await factoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondCall = Task.Run(() => registry.Lease(root));
        releaseFactory.Set();

        var first = await firstCall.WaitAsync(TimeSpan.FromSeconds(15));
        var second = await secondCall.WaitAsync(TimeSpan.FromSeconds(15));
        using var firstLease = first.Lease;
        using var secondLease = second.Lease;

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, factory.InstancesCreated);
        Assert.Same(firstLease!.Server, secondLease!.Server);
    }

    [Fact]
    public async Task Lease_DuringRunningBackgroundLoad_OtherRootsStayServiceable()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-hygiene-");
        var loadingRoot = CreateProjectRoot(tempDir, "loading");
        var otherRoot = CreateProjectRoot(tempDir, "other");
        var factory = new TrackingServerFactory();
        await using var registry = CreateRegistry(factory, new FakeClock());

        var loading = registry.Lease(loadingRoot);
        using var loadingLease = loading.Lease;
        Assert.Equal(ServerLoadState.Loading, loadingLease!.Server.LoadState);

        var other = await Task.Run(() => registry.Lease(otherRoot)).WaitAsync(TimeSpan.FromSeconds(15));
        using var otherLease = other.Lease;

        Assert.True(other.Succeeded);
        Assert.NotSame(loadingLease!.Server, otherLease!.Server);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.Equal(0, factory.LoadsCancelled);

        await registry.DisposeAsync();
        Assert.Equal(2, factory.LoadsCancelled);
    }

    [Fact]
    public async Task EvictionTick_IdleBeyondTtl_DisposesAndReloadsFresh()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-ttl-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, idleTtlMinutes: 15);

        var initial = registry.Lease(root);
        var serverInitial = initial.Lease!.Server;
        initial.Lease!.Dispose();
        clock.AdvanceMinutes(14);
        await registry.RunEvictionTickAsync();
        Assert.Equal(0, factory.LoadsCancelled);
        Assert.Equal(1, factory.InstancesCreated);

        var touched = registry.Lease(root);
        var serverTouched = touched.Lease!.Server;
        touched.Lease!.Dispose();

        clock.AdvanceMinutes(16);
        await registry.RunEvictionTickAsync();
        var reloaded = registry.Lease(root);
        using var reloadedLease = reloaded.Lease;

        Assert.Same(serverInitial, serverTouched);
        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(serverInitial, reloadedLease!.Server);
    }

    [Fact]
    public async Task EvictionTick_BusyEntryMarkedPending_AdoptionDefersEvictionUntilIdleAndExpired()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-busy-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, idleTtlMinutes: 15);

        var held = registry.Lease(root);
        using var heldLease = held.Lease;
        clock.AdvanceMinutes(20);
        await registry.RunEvictionTickAsync();

        var adopted = registry.Lease(root);
        using var adoptedLease = adopted.Lease;
        Assert.Same(heldLease!.Server, adoptedLease!.Server);
        heldLease!.Dispose();
        adoptedLease!.Dispose();
        clock.AdvanceMinutes(5);
        await registry.RunEvictionTickAsync();
        var rescued = registry.Lease(root);
        Assert.Same(heldLease!.Server, rescued.Lease!.Server);
        rescued.Lease!.Dispose();
        Assert.Equal(0, factory.LoadsCancelled);
        Assert.Equal(1, factory.InstancesCreated);

        clock.AdvanceMinutes(16);
        await registry.RunEvictionTickAsync();
        var reloaded = registry.Lease(root);
        using var reloadedLease = reloaded.Lease;

        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(heldLease!.Server, reloadedLease!.Server);
    }

    [Fact]
    public async Task EvictionTick_PendingWithoutAdoption_DisposedOnNextTick()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-pending-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, idleTtlMinutes: 15);

        var held = registry.Lease(root);
        using var heldLease = held.Lease;
        clock.AdvanceMinutes(20);
        await registry.RunEvictionTickAsync();
        Assert.Equal(0, factory.LoadsCancelled);

        heldLease!.Dispose();
        await registry.RunEvictionTickAsync();
        var reloaded = registry.Lease(root);
        using var reloadedLease = reloaded.Lease;

        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(heldLease!.Server, reloadedLease!.Server);
    }

    [Fact]
    public async Task Lease_AtCapacity_EvictsLeastRecentlyTouched()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-lru-");
        var rootA = CreateProjectRoot(tempDir, "alpha");
        var rootB = CreateProjectRoot(tempDir, "beta");
        var rootC = CreateProjectRoot(tempDir, "gamma");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, maxProjects: 2);

        var firstA = registry.Lease(rootA);
        var serverA = firstA.Lease!.Server;
        firstA.Lease!.Dispose();
        clock.AdvanceMinutes(5);
        var firstB = registry.Lease(rootB);
        var serverB = firstB.Lease!.Server;
        firstB.Lease!.Dispose();
        clock.AdvanceMinutes(5);
        var retouched = registry.Lease(rootA);
        Assert.Same(serverA, retouched.Lease!.Server);
        retouched.Lease!.Dispose();
        clock.AdvanceMinutes(1);

        var third = registry.Lease(rootC);
        using var thirdLease = third.Lease;
        Assert.Equal(3, factory.InstancesCreated);
        Assert.Equal(1, factory.LoadsCancelled);

        var againA = registry.Lease(rootA);
        using var againALease = againA.Lease;
        Assert.Same(serverA, againALease!.Server);

        var againB = registry.Lease(rootB);
        using var againBLease = againB.Lease;

        Assert.NotSame(serverB, againBLease!.Server);
        Assert.Equal(4, factory.InstancesCreated);
    }

    [Fact]
    public async Task Lease_LruEviction_SkipsBusyEntriesUntilReleased()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-lru-busy-");
        var rootA = CreateProjectRoot(tempDir, "alpha");
        var rootB = CreateProjectRoot(tempDir, "beta");
        var rootC = CreateProjectRoot(tempDir, "gamma");
        var factory = new TrackingServerFactory();
        var clock = new FakeClock();
        await using var registry = CreateRegistry(factory, clock, maxProjects: 1);

        var held = registry.Lease(rootA);
        using var heldLease = held.Lease;
        var serverA = heldLease!.Server;
        clock.AdvanceMinutes(1);
        var overflowing = registry.Lease(rootB);
        var serverB = overflowing.Lease!.Server;
        overflowing.Lease!.Dispose();
        var stillResident = registry.Lease(rootA);
        Assert.Same(serverA, stillResident.Lease!.Server);
        stillResident.Lease!.Dispose();

        Assert.Equal(2, factory.InstancesCreated);
        Assert.Equal(0, factory.LoadsCancelled);

        heldLease!.Dispose();
        clock.AdvanceMinutes(1);
        var third = registry.Lease(rootC);
        using var thirdLease = third.Lease;
        var recreated = registry.Lease(rootB);
        using var recreatedLease = recreated.Lease;

        Assert.Equal(2, factory.LoadsCancelled);
        Assert.Equal(4, factory.InstancesCreated);
        Assert.NotSame(serverB, recreatedLease!.Server);
    }

    [Fact]
    public async Task Lease_AfterFailedColdLoad_NextHitStartsFreshLoad()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-failed-hit-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory { FailLoads = true };
        await using var registry = CreateRegistry(factory, new FakeClock());

        var failed = registry.Lease(root);
        var failedServer = failed.Lease!.Server;
        await Assert.ThrowsAsync<InvalidOperationException>(() => failedServer.LoadTask!);
        Assert.Equal(ServerLoadState.LoadFailed, failedServer.LoadState);
        failed.Lease!.Dispose();

        factory.FailLoads = false;
        var retry = registry.Lease(root);
        using var retryLease = retry.Lease;

        Assert.True(failed.Succeeded);
        Assert.True(retry.Succeeded);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(failedServer, retryLease!.Server);
        Assert.Equal(ServerLoadState.Loading, retryLease!.Server.LoadState);
    }

    [Fact]
    public async Task EvictionTick_RemovesFailedMarker_IndependentOfLastUsed()
    {
        using var tempDir = TestTempDirectory.Create("project-registry-failed-tick-");
        var root = CreateProjectRoot(tempDir, "proj");
        var factory = new TrackingServerFactory { FailLoads = true };
        await using var registry = CreateRegistry(factory, new FakeClock());

        var failed = registry.Lease(root);
        var failedServer = failed.Lease!.Server;
        await Assert.ThrowsAsync<InvalidOperationException>(() => failedServer.LoadTask!);
        failed.Lease!.Dispose();
        await registry.RunEvictionTickAsync();

        factory.FailLoads = false;
        var reloaded = registry.Lease(root);
        using var reloadedLease = reloaded.Lease;

        Assert.Equal(ServerLoadState.LoadFailed, failedServer.LoadState);
        Assert.Equal(1, factory.LoadsCancelled);
        Assert.Equal(2, factory.InstancesCreated);
        Assert.NotSame(failedServer, reloadedLease!.Server);
        Assert.Equal(ServerLoadState.Loading, reloadedLease!.Server.LoadState);
    }

    private static ProjectRegistry CreateRegistry(
        TrackingServerFactory factory,
        FakeClock clock,
        int maxProjects = ProjectRegistryDefaults.MaxProjects,
        int idleTtlMinutes = 45)
    {
        return new ProjectRegistry(new ProjectRegistryOptions(
            factory.Factory,
            clock,
            maxProjects,
            TimeSpan.FromMinutes(idleTtlMinutes)));
    }

    private static string CreateProjectRoot(TestTempDirectory tempDir, string name)
    {
        var root = Path.Combine(tempDir.DirectoryPath, name);
        tempDir.CreateFile(Path.Combine(name, "app.slnx"), string.Empty);
        tempDir.CreateFile(Path.Combine(name, "rules.json"), "{}");
        tempDir.CreateFile(
            Path.Combine(name, "ainetlinter.project.json"),
            "{ \"solution\": \"app.slnx\", \"rules\": \"rules.json\" }");
        return root;
    }
}

[Trait("Category", "Unit")]
internal sealed class FakeClock : TimeProvider
{
    private long utcTicks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

    public override DateTimeOffset GetUtcNow() => new(Volatile.Read(ref utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan delta) => Interlocked.Add(ref utcTicks, delta.Ticks);

    public void AdvanceMinutes(int minutes) => Advance(TimeSpan.FromMinutes(minutes));
}

[Trait("Category", "Unit")]
internal sealed class TrackingServerFactory
{
    private int instancesCreated;
    private int loadsCancelled;
    private int failLoads;

    internal int InstancesCreated => instancesCreated;

    internal int LoadsCancelled => loadsCancelled;

    internal bool FailLoads
    {
        get => Volatile.Read(ref failLoads) == 1;
        set => Volatile.Write(ref failLoads, value ? 1 : 0);
    }

    internal Func<ProjectDefinition, ProjectInstanceCreation> Factory =>
        definition => ProjectInstanceCreation.Resident(CreateServer(definition));

    internal McpCodeGraphServer CreateServer(ProjectDefinition definition)
    {
        Interlocked.Increment(ref instancesCreated);
        return FailLoads ? CreateFailedLoadServer() : CreatePendingLoadServer();
    }

    internal McpCodeGraphServer CreatePendingLoadServer()
    {
        return new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = MinimalConfig(),
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() =>
                {
                    Interlocked.Increment(ref loadsCancelled);
                    pending.TrySetCanceled(token);
                });
                return pending.Task;
            },
        });
    }

    private McpCodeGraphServer CreateFailedLoadServer()
    {
        return new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = MinimalConfig(),
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                token.Register(() => Interlocked.Increment(ref loadsCancelled));
                return Task.FromException<SourceFileCatalog?>(new InvalidOperationException("Katalog kann nicht geladen werden."));
            },
        });
    }

    private static Config MinimalConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };
}
