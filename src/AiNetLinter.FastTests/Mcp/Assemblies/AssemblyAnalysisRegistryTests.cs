#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisRegistry
// @covers AssemblyAnalysisEntry
public sealed class AssemblyAnalysisRegistryTests
{
    [Fact]
    public async Task LeaseAsync_CancellationDoesNotCancelSharedCreation()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-cancel-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryCancellation",
            "namespace Probe; public sealed class Value { }");
        await using var registry = new AssemblyAnalysisRegistry();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => registry.LeaseAsync(assemblyPath, cancellation.Token));

        var successful = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(successful.Lease);
        Assert.Equal(1, registry.ResidentCount);
        successful.Lease!.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_ConcurrentWaiters_CancelledWaiterThrowsWhileOtherCompletes()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-concurrent-cancel-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryConcurrentCancel",
            "namespace Probe; public sealed class Value { public int X => 1; }");
        await using var registry = new AssemblyAnalysisRegistry();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelledTask = registry.LeaseAsync(assemblyPath, cancellation.Token);
        var successTask = registry.LeaseAsync(assemblyPath, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledTask);
        var successful = await successTask;

        Assert.NotNull(successful.Lease);
        Assert.Equal(1, registry.ResidentCount);
        successful.Lease!.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_InternalCreationAbortRemovesEntryAndSubsequentAttemptSucceeds()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-internal-cancel-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryInternalCancel",
            "namespace Probe; public sealed class Value { }");
        var attempts = 0;
        var orchestrator = new TestRegistrySourceResolver((_, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new OperationCanceledException("internal creation aborted");
            }
            return Task.FromResult(new AssemblySourceResolution(null, null, []));
        });
        await using var registry = new AssemblyAnalysisRegistry(orchestrator);

        var failed = await registry.LeaseAsync(assemblyPath, CancellationToken.None);

        Assert.Null(failed.Lease);
        Assert.NotNull(failed.Error);
        Assert.Contains("abgebrochen", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(failed.Error!.Content)).Text);

        var retry = await registry.LeaseAsync(assemblyPath, CancellationToken.None);
        Assert.NotNull(retry.Lease);
        Assert.Equal(1, registry.ResidentCount);
        retry.Lease!.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_ConcurrentFirstAccessUsesOneCreationBarrier()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-creation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryCreation",
            "namespace Probe; public sealed class Value { }");
        await using var registry = new AssemblyAnalysisRegistry();

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => registry.LeaseAsync(assemblyPath)));

        Assert.All(results, result => Assert.NotNull(result.Lease));
        Assert.Single(results.Select(result => result.Lease!.Server).Distinct());
        Assert.Equal(1, registry.ResidentCount);
        foreach (var result in results) result.Lease!.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_ChangedBytesPublishNewEntryWhileOldLeaseRemainsReadable()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-generation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryGeneration",
            "namespace Probe; public sealed class First { }");
        await using var registry = new AssemblyAnalysisRegistry();

        var firstResult = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(firstResult.Lease);
        var firstLease = firstResult.Lease!;
        var firstHash = firstLease.Context.Origin.ContentHash;
        Assert.NotNull(firstLease.Context.Compilation.GetTypeByMetadataName("Probe.First"));

        AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryGeneration",
            "namespace Probe; public sealed class Second { }");
        var secondResult = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(secondResult.Lease);
        var secondLease = secondResult.Lease!;

        Assert.NotSame(firstLease.Server, secondLease.Server);
        Assert.NotEqual(firstHash, secondLease.Context.Origin.ContentHash);
        Assert.NotNull(firstLease.Context.Compilation.GetTypeByMetadataName("Probe.First"));
        Assert.Null(firstLease.Context.Compilation.GetTypeByMetadataName("Probe.Second"));
        Assert.NotNull(secondLease.Context.Compilation.GetTypeByMetadataName("Probe.Second"));

        secondLease.Dispose();
        firstLease.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_AbaRefreshKeepsGenerationMonotonicAndRejectsStaleResolverId()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-aba-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryAba",
            "namespace Probe; public sealed class First { public void Run() { } }");
        await using var registry = new AssemblyAnalysisRegistry();

        var first = (await registry.LeaseAsync(assemblyPath)).Lease!;
        var firstSymbol = first.Context.Compilation.GetTypeByMetadataName("Probe.First")!;
        var firstId = CallGraphTraversal.GetStableSymbolId(firstSymbol, first.Server.AssemblySymbolIdentity);
        var firstGeneration = first.Context.Generation;

        AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryAba",
            "namespace Probe; public sealed class Second { public void Run() { } }");
        var second = (await registry.LeaseAsync(assemblyPath)).Lease!;
        var secondGeneration = second.Context.Generation;

        second.Dispose();
        AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryAba",
            "namespace Probe; public sealed class First { public void Run() { } }");
        var third = (await registry.LeaseAsync(assemblyPath)).Lease!;
        var thirdGeneration = third.Context.Generation;

        Assert.True(firstGeneration < secondGeneration);
        Assert.True(secondGeneration < thirdGeneration);
        Assert.NotEqual(first.Server.AssemblySymbolIdentity, third.Server.AssemblySymbolIdentity);

        var (staleSymbol, staleError) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            third.Server.GetCurrentSolution()!,
            firstId,
            CancellationToken.None,
            third.Server.AssemblySymbolIdentity);

        Assert.Null(staleSymbol);
        Assert.NotNull(staleError);
        Assert.Contains(
            "aktuellen Assembly-Generation",
            Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(staleError!.Content)).Text);

        third.Dispose();
        first.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_MtimeOnlyChangeReusesExistingEntry()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-mtime-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "RegistryMtime",
            "namespace Probe; public sealed class Value { }");
        await using var registry = new AssemblyAnalysisRegistry();

        var firstResult = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(firstResult.Lease);
        var firstLease = firstResult.Lease!;
        var originalMtime = File.GetLastWriteTimeUtc(assemblyPath);
        firstLease.Dispose();
        File.SetLastWriteTimeUtc(assemblyPath, originalMtime.AddMinutes(1));

        var secondResult = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(secondResult.Lease);
        var secondLease = secondResult.Lease!;

        Assert.Same(firstLease.Server, secondLease.Server);
        Assert.Equal(firstLease.Context.Origin.ContentHash, secondLease.Context.Origin.ContentHash);
        secondLease.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_ExternalCapacityIsSeparateAndVisibleWithoutEvictingActiveLease()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-capacity-");
        var firstPath = AssemblyTestHelper.EmitAssembly(temp, "CapacityFirst", "namespace Probe; public sealed class First { }");
        var secondPath = AssemblyTestHelper.EmitAssembly(temp, "CapacitySecond", "namespace Probe; public sealed class Second { }");
        using var resources = new ExternalResourceRegistry(new ExternalResourceRegistryOptions(MaxResidentResources: 1));
        await using var registry = new AssemblyAnalysisRegistry(resourceRegistry: resources);

        var first = await registry.LeaseAsync(firstPath);
        var rejected = await registry.LeaseAsync(secondPath);

        Assert.NotNull(first.Lease);
        Assert.Null(rejected.Lease);
        Assert.Contains("Ressourcen", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(rejected.Error!.Content)).Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ExternalResourceHealth.CapacityExceeded, resources.Health.Health);
        Assert.Equal(1, registry.ResidentCount);

        first.Lease!.Dispose();
    }

    [Fact]
    public async Task LeaseAsync_StableChurnStopsAfterBoundedFingerprintRetries()
    {
        using var temp = TestTempDirectory.Create("assembly-registry-churn-");
        var variants = Enumerable.Range(0, AssemblyAnalysisRegistry.MaxFingerprintRetries + 2)
            .Select(index => AssemblyTestHelper.EmitAssembly(
                temp,
                $"RegistryChurn{index}",
                $"namespace Probe; public sealed class Value{index} {{ }}"))
            .ToArray();
        var assemblyPath = temp.GetPath("RegistryChurnTarget.dll");
        File.Copy(variants[0], assemblyPath);

        var fingerprintReads = 0;
        await using var registry = new AssemblyAnalysisRegistry(
            fingerprintFactory: path =>
            {
                var read = Interlocked.Increment(ref fingerprintReads);
                if (read > 1 && read < variants.Length)
                {
                    File.Copy(variants[read], path, overwrite: true);
                }

                var fingerprint = AssemblyFingerprintCalculator.Create(path);
                if (read == 1)
                {
                    File.Copy(variants[1], path, overwrite: true);
                }

                return fingerprint;
            });

        var result = await registry.LeaseAsync(assemblyPath);

        Assert.Equal(AssemblyAnalysisRegistry.MaxFingerprintRetries + 1, fingerprintReads);
        Assert.Null(result.Lease);
        Assert.NotNull(result.Error);
        Assert.Contains(
            "kontrolliert abgebrochen",
            Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Error!.Content)).Text);
        Assert.Equal(1, registry.ResidentCount);
    }

    [Fact]
    public async Task Entry_DisposeWaitsForLeasesRejectsNewLeasesAndIsIdempotent()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            "namespace EntryTest; public sealed class Value { }");
        var context = await CreateContextAsync(solution.Solution);
        var lifetime = new TrackingLifetime();
        var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
            "entry-test.dll",
            solution.Solution,
            context,
            lifetime));

        Assert.True(entry.TryAcquireLease(out var lease));
        var firstDispose = entry.DisposeAsync().AsTask();
        var secondDispose = entry.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        Assert.False(firstDispose.IsCompleted);
        Assert.False(entry.TryAcquireLease(out _));

        lease!.Dispose();
        await firstDispose;

        Assert.Equal(1, lifetime.DisposeCount);
        Assert.False(entry.TryAcquireLease(out _));
    }

    [Fact]
    public async Task Entry_DisposeReportsLifetimeFailureAfterServerCleanupAttempt()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            "namespace EntryTest; public sealed class Value { }");
        var context = await CreateContextAsync(solution.Solution);
        var lifetime = new TrackingLifetime(throws: true);
        var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
            "entry-failure-test.dll",
            solution.Solution,
            context,
            lifetime));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => entry.DisposeAsync().AsTask());

        Assert.Equal("lifetime", failure.Message);
        Assert.Equal(1, lifetime.DisposeCount);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => entry.DisposeAsync().AsTask());
        Assert.Equal(1, lifetime.DisposeCount);
    }

    private static async Task<AssemblyContext> CreateContextAsync(Solution solution)
    {
        var project = solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        return new AssemblyContext(
            compilation.Assembly,
            new AssemblyIdentityDto("EntryTest", "1.0.0.0", "", ""),
            [],
            [],
            compilation,
            null,
            null,
            new AssemblyOrigin("test", "entry-test.dll", "test-hash", "", "high"),
            1,
            AssemblySessionStatus.Complete);
    }

    private sealed class TrackingLifetime(bool throws = false) : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (throws) throw new InvalidOperationException("lifetime");
        }
    }

    private sealed class TestRegistrySourceResolver(
        Func<string, CancellationToken, Task<AssemblySourceResolution>> resolveFunc) : IAssemblySourceResolver
    {
        public Task<AssemblySourceResolution> ResolveForRegistryAsync(string assemblyPath, CancellationToken cancellationToken) =>
            resolveFunc(assemblyPath, cancellationToken);
    }

}
