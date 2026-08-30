#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
public sealed class SourceSnapshotRegistryTests
{
    private const string FirstSnapshotLabel = "alpha";
    private const string SecondSnapshotLabel = "omega";

    [Fact]
    public void Acquire_DeduplicatesAssemblyAliasesAndDisposesOnlyDuplicateOwner()
    {
        var firstMapping = new ExternalSourceMapping(
            "HTTPS://GITEA.EXAMPLE/shared.git",
            "src/Shared.slnx",
            ["First"]);
        var aliasMapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            @".\src\Shared.slnx",
            ["Second", "Third"]);
        using var firstSnapshot = CreateSnapshot(firstMapping, "revision-1");
        using var duplicateSnapshot = CreateSnapshot(aliasMapping, "revision-1");
        using var registry = new SourceSnapshotRegistry();

        using var firstLease = registry.Acquire(firstSnapshot);
        using var duplicateLease = registry.Acquire(duplicateSnapshot);

        Assert.Equal(1, registry.ResidentCount);
        Assert.Same(firstSnapshot, firstLease.Snapshot);
        Assert.Same(firstSnapshot, duplicateLease.Snapshot);
        Assert.False(firstSnapshot.IsDisposed);
        Assert.True(duplicateSnapshot.IsDisposed);
    }

    [Fact]
    public void Acquire_SeparatesRevisionAndSolutionPath()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["First"]);
        var otherSolutionMapping = new ExternalSourceMapping(
            mapping.Url,
            "src/Other.sln",
            mapping.Assemblies);
        using var firstSnapshot = CreateSnapshot(mapping, "revision-1");
        using var otherRevision = CreateSnapshot(mapping, "revision-2");
        using var otherSolution = CreateSnapshot(otherSolutionMapping, "revision-1");
        using var registry = new SourceSnapshotRegistry();

        using var firstLease = registry.Acquire(firstSnapshot);
        using var revisionLease = registry.Acquire(otherRevision);
        using var solutionLease = registry.Acquire(otherSolution);

        Assert.Equal(3, registry.ResidentCount);
        Assert.Same(firstSnapshot, firstLease.Snapshot);
        Assert.Same(otherRevision, revisionLease.Snapshot);
        Assert.Same(otherSolution, solutionLease.Snapshot);
    }

    [Fact]
    public void LeaseAndRegistryDisposeAreIdempotentAndRegistryDisposeIsTerminal()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["First"]);
        using var snapshot = CreateSnapshot(mapping, "revision-1");
        var registry = new SourceSnapshotRegistry();
        var lease = registry.Acquire(snapshot);

        lease.Dispose();
        lease.Dispose();
        Assert.Equal(1, registry.ResidentCount);
        Assert.False(snapshot.IsDisposed);

        registry.Dispose();
        registry.Dispose();

        Assert.Equal(0, registry.ResidentCount);
        Assert.True(snapshot.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => registry.Acquire(snapshot));

    }

    [Fact]
    public void Dispose_ContinuesAfterSnapshotFailureAndDoesNotRetryOnSecondCall()
    {
        var disposeOrder = new List<string>();
        var firstMapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Alpha.slnx",
            ["First"]);
        var secondMapping = new ExternalSourceMapping(
            firstMapping.Url,
            "src/Omega.slnx",
            ["Second"]);
        var firstOwner = new TrackingCheckoutOwner(FirstSnapshotLabel, disposeOrder, throws: true);
        var secondOwner = new TrackingCheckoutOwner(SecondSnapshotLabel, disposeOrder, throws: false);
        using var firstSnapshot = CreateSnapshot(firstMapping, "revision-1", firstOwner);
        using var secondSnapshot = CreateSnapshot(secondMapping, "revision-1", secondOwner);
        using var registry = new SourceSnapshotRegistry();
        using var secondLease = registry.Acquire(secondSnapshot);
        using var firstLease = registry.Acquire(firstSnapshot);
        Assert.True(
            string.CompareOrdinal(
                firstSnapshot.Identity.StableValue,
                secondSnapshot.Identity.StableValue) < 0,
            $"first={firstSnapshot.Identity.StableValue}; second={secondSnapshot.Identity.StableValue}");

        registry.Dispose();

        Assert.Empty(disposeOrder);
        Assert.False(firstSnapshot.IsDisposed);
        Assert.False(secondSnapshot.IsDisposed);
        Assert.Equal(2, registry.ResidentCount);

        secondLease.Dispose();
        var failure = Assert.Throws<InvalidOperationException>(() => firstLease.Dispose());

        Assert.Equal(FirstSnapshotLabel, failure.Message);
        Assert.Equal([SecondSnapshotLabel, FirstSnapshotLabel], disposeOrder);
        Assert.True(firstSnapshot.IsDisposed);
        Assert.True(secondSnapshot.IsDisposed);
        Assert.Equal(0, registry.ResidentCount);
        Assert.Equal(1, firstOwner.DisposeCount);
        Assert.Equal(1, secondOwner.DisposeCount);

        registry.Dispose();

        Assert.Equal(1, firstOwner.DisposeCount);
        Assert.Equal(1, secondOwner.DisposeCount);
        Assert.Equal(0, registry.ResidentCount);

        firstSnapshot.Dispose();
        secondSnapshot.Dispose();
    }

    [Fact]
    public void Dispose_DrainsActiveLeasesAndReportsEachSnapshotFailure()
    {
        var disposeOrder = new List<string>();
        var firstMapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Alpha.slnx",
            ["First"]);
        var secondMapping = new ExternalSourceMapping(
            firstMapping.Url,
            "src/Omega.slnx",
            ["Second"]);
        var firstOwner = new TrackingCheckoutOwner(FirstSnapshotLabel, disposeOrder, throws: true);
        var secondOwner = new TrackingCheckoutOwner(SecondSnapshotLabel, disposeOrder, throws: true);
        using var firstSnapshot = CreateSnapshot(firstMapping, "revision-1", firstOwner);
        using var secondSnapshot = CreateSnapshot(secondMapping, "revision-1", secondOwner);
        using var registry = new SourceSnapshotRegistry();
        using var secondLease = registry.Acquire(secondSnapshot);
        using var firstLease = registry.Acquire(firstSnapshot);

        registry.Dispose();

        Assert.Equal(2, registry.ResidentCount);
        var secondFailure = Assert.Throws<InvalidOperationException>(() => secondLease.Dispose());
        var firstFailure = Assert.Throws<InvalidOperationException>(() => firstLease.Dispose());

        Assert.Equal(SecondSnapshotLabel, secondFailure.Message);
        Assert.Equal(FirstSnapshotLabel, firstFailure.Message);
        Assert.Equal([SecondSnapshotLabel, FirstSnapshotLabel], disposeOrder);
        Assert.Equal(1, firstOwner.DisposeCount);
        Assert.Equal(1, secondOwner.DisposeCount);

        registry.Dispose();

        Assert.Equal(1, firstOwner.DisposeCount);
        Assert.Equal(1, secondOwner.DisposeCount);
        Assert.Equal(0, registry.ResidentCount);

        firstSnapshot.Dispose();
        secondSnapshot.Dispose();
    }

    [Fact]
    public void Acquire_UsesIndependentResourceBudgetAndDisposesEvictedSnapshot()
    {
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(MaxResidentResources: 1));
        using var registry = new SourceSnapshotRegistry(resources);
        var first = CreateSnapshot(
            new ExternalSourceMapping("https://gitea.example/shared.git", "src/First.slnx", ["First"]),
            "revision-1");
        var second = CreateSnapshot(
            new ExternalSourceMapping("https://gitea.example/shared.git", "src/Second.slnx", ["Second"]),
            "revision-1");

        using (var firstLease = registry.Acquire(first))
        {
            firstLease.Dispose();
        }

        using var secondLease = registry.Acquire(second);

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);
        Assert.Equal(1, registry.Health.ResidentResources);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void EvictIdle_DisposesReleasedSnapshotButPreservesActiveLease()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            IdleTtl: TimeSpan.FromMinutes(1),
            Clock: clock));
        using var registry = new SourceSnapshotRegistry(resources);
        var released = CreateSnapshot(
            new ExternalSourceMapping("https://gitea.example/shared.git", "src/Released.slnx", ["Released"]),
            "revision-1");
        var active = CreateSnapshot(
            new ExternalSourceMapping("https://gitea.example/shared.git", "src/Active.slnx", ["Active"]),
            "revision-1");
        using var releasedLease = registry.Acquire(released);
        releasedLease.Dispose();
        using var activeLease = registry.Acquire(active);

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, registry.EvictIdle());
        Assert.True(released.IsDisposed);
        Assert.False(active.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);

        activeLease.Dispose();
        released.Dispose();
        active.Dispose();
    }

    private static ExternalSourceSnapshot CreateSnapshot(
        ExternalSourceMapping mapping,
        string revision,
        IExternalSourceCheckoutOwner? checkoutOwner = null)
    {
        var workspace = new AdhocWorkspace();
        return new(
            SourceSnapshotIdentity.Create(mapping, revision),
            workspace.CurrentSolution,
            workspace,
            new ExternalSourceSnapshotOwnership(checkoutOwner));
    }

    private sealed class TrackingCheckoutOwner(
        string name,
        List<string> disposeOrder,
        bool throws) : IExternalSourceCheckoutOwner
    {
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            disposeOrder.Add(name);
            if (throws)
            {
                throw new InvalidOperationException(name);
            }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset utcNow = initial;

        internal void Advance(TimeSpan value) => utcNow += value;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

[Trait("Category", "Unit")]
public sealed class SourceSnapshotIdentityTests
{
    [Fact]
    public void Identity_CanonicalizesRepositorySolutionAndRevisionComponents()
    {
        var mapping = new ExternalSourceMapping(
            " HTTPS://GITEA.EXAMPLE/shared.git ",
            @".\src\..\src/Shared.slnx",
            ["First"]);

        var identity = SourceSnapshotIdentity.Create(mapping, " revision-1 ");

        Assert.Equal("https://gitea.example/shared.git", identity.RepositoryUrl);
        Assert.Equal("revision-1", identity.LoadedRevision);
        Assert.Equal("src/Shared.slnx", identity.SolutionPath);
        Assert.Equal(
            "32:https://gitea.example/shared.git|10:revision-1|15:src/Shared.slnx",
            identity.StableValue);
    }

    [Fact]
    public void Identity_RejectsEmptyRevisionAndRepositoryEscapes()
    {
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["First"]);
        var escapingMapping = new ExternalSourceMapping(
            mapping.Url,
            "../../Shared.slnx",
            mapping.Assemblies);

        Assert.Throws<ArgumentException>(() => SourceSnapshotIdentity.Create(mapping, "  "));
        Assert.Throws<ArgumentException>(() => SourceSnapshotIdentity.Create(escapingMapping, "revision-1"));
    }
}
