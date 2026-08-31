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
}
