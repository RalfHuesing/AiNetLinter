#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisRegistry
// @covers AssemblyAnalysisEntry
// @covers AssemblyAnalysisRegistryEvictionCoordinator
// @covers AssemblyAnalysisSourceProjectLeaseCoordinator
public sealed class AssemblyAnalysisRegistryRetirementRaceTests
{
    [Fact]
    public async Task LeaseAsync_CapacityRetirementRevalidatesLeaseAcquiredAfterIdleCheck()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-retirement-race-");
        var firstPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RetirementRaceFirst",
            "namespace Probe; public sealed class First { }");
        var secondPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RetirementRaceSecond",
            "namespace Probe; public sealed class Second { }");
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(
            MaxResidentResources: 1));
        var candidateChecked = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRetirement = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AssemblyAnalysisLease? activeLease = null;
        await using var registry = new AssemblyAnalysisRegistry(
            resourceRegistry: resources,
            beforeRetirementAsync: async entry =>
            {
                Assert.True(entry.IsIdleForCapacity());
                Assert.True(entry.TryAcquireLease(out activeLease));
                candidateChecked.TrySetResult(null);
                await continueRetirement.Task.ConfigureAwait(false);
            });
        try
        {
            var first = await registry.LeaseAsync(firstPath);
            Assert.NotNull(first.Lease);
            first.Lease!.Dispose();

            var second = registry.LeaseAsync(secondPath);
            await candidateChecked.Task;
            Assert.NotNull(activeLease);
            continueRetirement.SetResult(null);

            var result = await second;

            Assert.Null(result.Lease);
            Assert.NotNull(result.Error);
            Assert.Equal(1, registry.ResidentCount);
            Assert.Equal(1, resources.ResidentCount);
        }
        finally
        {
            continueRetirement.TrySetResult(null);
            activeLease?.Dispose();
        }
    }

    [Fact]
    public async Task LeaseAsync_FingerprintRefreshClearsPendingRequestForRetiredEntry()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-reference-eviction-refresh-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceEvictionRefreshDependency",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceEvictionRefreshRoot",
            "namespace Probe; public sealed class Root { public DependencyType Value { get; } = new(); }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        using var foreignLease = (await registry.LeaseAsync(dependencyPath)).Lease!;
        using var rootLease = (await registry.LeaseAsync(rootPath)).Lease!;
        await rootLease.ExpandReferencesAsync();
        rootLease.Dispose();

        Assert.True(registry.TemporaryReferenceEvictionRequestCount > 0);
        await ((IAssemblyAnalysisTemporaryReferenceEvictor)registry)
            .EvictTemporaryReferenceSessionsAsync();
        Assert.True(registry.TemporaryReferenceEvictionRequestCount > 0);

        var previousGeneration = foreignLease.Context.Generation;
        AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceEvictionRefreshDependency",
            "namespace Probe; public sealed class DependencyType { public int Value => 2; public int Changed => 3; }");

        using var refreshed = (await registry.LeaseAsync(dependencyPath)).Lease!;
        Assert.True(refreshed.Context.Generation > previousGeneration);
        Assert.Equal(0, registry.TemporaryReferenceEvictionRequestCount);
    }

    [Fact]
    public async Task DisposeAsync_ClearsPendingRequestForEntryHeldByForeignLease()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-reference-eviction-dispose-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceEvictionDisposeDependency",
            "namespace Probe; public sealed class DependencyType { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ReferenceEvictionDisposeRoot",
            "namespace Probe; public sealed class Root { public DependencyType Value { get; } = new(); }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        using var foreignLease = (await registry.LeaseAsync(dependencyPath)).Lease!;
        using var rootLease = (await registry.LeaseAsync(rootPath)).Lease!;
        await rootLease.ExpandReferencesAsync();
        rootLease.Dispose();

        Assert.True(registry.TemporaryReferenceEvictionRequestCount > 0);
        var disposal = registry.DisposeAsync().AsTask();
        foreignLease.Dispose();

        await disposal;
        Assert.Equal(0, registry.TemporaryReferenceEvictionRequestCount);
    }
}
