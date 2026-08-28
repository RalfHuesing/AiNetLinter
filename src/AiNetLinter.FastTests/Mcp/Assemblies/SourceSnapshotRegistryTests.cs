#nullable enable

using System;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
public sealed class SourceSnapshotRegistryTests
{
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

    private static ExternalSourceSnapshot CreateSnapshot(
        ExternalSourceMapping mapping,
        string revision)
    {
        var workspace = new AdhocWorkspace();
        return new(
            SourceSnapshotIdentity.Create(mapping, revision),
            workspace.CurrentSolution,
            workspace);
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
