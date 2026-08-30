#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolSupportTests
{
    [Fact]
    public async Task ResolveAsync_ConcurrentFirstAccessSharesProviderCreationAndSnapshot()
    {
        using var temp = TestTempDirectory.Create("assembly-source-creation-barrier-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = CreateMapping(["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec(
                "SourceProject",
                "TargetAssembly",
                "namespace Source; public sealed class SourceOnly { }"));
        using var registry = new SourceSnapshotRegistry();
        var provider = new BlockingProvider(snapshot);
        var orchestrator = CreateConfiguredOrchestrator(temp, ["TargetAssembly"], provider, registry);

        var first = orchestrator.ResolveAsync(assemblyPath);
        await provider.Started.Task;
        var second = orchestrator.ResolveAsync(assemblyPath);

        Assert.Equal(1, provider.CallCount);
        provider.Release.SetResult(null);

        using var firstScope = await first;
        using var secondScope = await second;

        Assert.Equal(1, provider.CallCount);
        Assert.NotNull(firstScope.Selection);
        Assert.NotNull(secondScope.Selection);
        Assert.Same(
            firstScope.Selection!.SourceLease.Snapshot,
            secondScope.Selection!.SourceLease.Snapshot);
        Assert.Equal(1, registry.ResidentCount);

        orchestrator.Dispose();
    }

    private sealed class BlockingProvider(ExternalSourceSnapshot snapshot) : IExternalSourceProvider
    {
        internal readonly TaskCompletionSource<object?> Started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<object?> Release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int callCount;

        internal int CallCount => Volatile.Read(ref callCount);

        public async ValueTask<ExternalSourceProviderResult> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref callCount);
            Started.TrySetResult(null);
            await Release.Task.WaitAsync(cancellationToken);
            return new ExternalSourceProviderResult(
                true,
                [],
                snapshot,
                ExternalSourceRepositoryResultState.Create(
                    health: ExternalSourceRepositoryHealth.Verified,
                    checkoutTrust: ExternalSourceCheckoutTrust.Clean));
        }
    }
}
