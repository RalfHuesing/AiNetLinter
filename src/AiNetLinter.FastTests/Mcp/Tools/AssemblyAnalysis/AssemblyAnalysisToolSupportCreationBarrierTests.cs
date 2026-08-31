#nullable enable

using System;
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

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task ResolveAsync_ConsumerCancellationDoesNotCancelSharedProducer()
    {
        using var temp = TestTempDirectory.Create("assembly-source-creation-consumer-cancel-");
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
        using var cancellation = new CancellationTokenSource();

        var first = orchestrator.ResolveAsync(assemblyPath, cancellation.Token);
        await provider.Started.Task;
        var second = orchestrator.ResolveAsync(assemblyPath);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(1, provider.CallCount);
        provider.Release.SetResult(null);

        using var secondScope = await second;
        Assert.NotNull(secondScope.Selection);
        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_DuringSharedProviderCreationCancelsProducerAndWaiters()
    {
        using var temp = TestTempDirectory.Create("assembly-source-creation-dispose-");
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
        await orchestrator.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(1, provider.CallCount);
        await provider.Completed.Task;
        Assert.True(provider.Completed.Task.IsCompletedSuccessfully);
        Assert.Equal(0, registry.ResidentCount);
    }

    [Fact]
    public async Task Dispose_AfterProviderCompletionStillJoinsBeforeCreationRemoval()
    {
        using var temp = TestTempDirectory.Create("assembly-source-creation-complete-race-");
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
        var completedBeforeRemoval = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRemoval = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var orchestrator = CreateConfiguredOrchestrator(
            temp,
            ["TargetAssembly"],
            provider,
            registry,
            async () =>
            {
                completedBeforeRemoval.TrySetResult(null);
                await allowRemoval.Task.ConfigureAwait(false);
            });

        try
        {
            var resolution = orchestrator.ResolveAsync(assemblyPath);
            await provider.Started.Task;
            provider.Release.SetResult(null);
            await completedBeforeRemoval.Task;

            var dispose = orchestrator.DisposeAsync().AsTask();
            Assert.False(dispose.IsCompleted);
            allowRemoval.SetResult(null);

            await dispose;
            using var scope = await resolution;
            Assert.NotNull(scope.Selection);
            Assert.Equal(1, provider.CallCount);
            Assert.Equal(1, registry.ResidentCount);
        }
        finally
        {
            allowRemoval.TrySetResult(null);
            await orchestrator.DisposeAsync();
        }
    }

    private sealed class BlockingProvider(ExternalSourceSnapshot snapshot) : IExternalSourceProvider
    {
        internal readonly TaskCompletionSource<object?> Started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<object?> Release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<object?> Completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int callCount;

        internal int CallCount => Volatile.Read(ref callCount);

        public async ValueTask<ExternalSourceProviderResult> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref callCount);
            Started.TrySetResult(null);
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
                return new ExternalSourceProviderResult(
                    true,
                    [],
                    snapshot,
                    ExternalSourceRepositoryResultState.Create(
                        health: ExternalSourceRepositoryHealth.Verified,
                        checkoutTrust: ExternalSourceCheckoutTrust.Clean));
            }
            finally
            {
                Completed.TrySetResult(null);
            }
        }
    }
}
