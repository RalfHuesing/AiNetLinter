#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisCacheTests
{
    [Fact]
    public void AssemblyDecompilationCache_DefaultRootUsesCacheContractPolicy()
    {
        var cache = new AssemblyDecompilationCache();
        var expected = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            AssemblyCacheContract.DefaultCacheDirectoryName,
            AssemblyCacheContract.DefaultAssemblyCacheDirectoryName));

        Assert.Equal(expected, AssemblyCacheContract.ResolveRootPath(null));
        Assert.Equal(expected, cache.RootPath);
    }

    [Fact]
    public async Task AssemblyDecompilationCache_ConcurrentPublishReturnsOnlyExistingGenerationDirectories()
    {
        using var temp = TestTempDirectory.Create("assembly-cache-publish-race-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CachePublishRaceProbe",
            "namespace Probe; public sealed class Value { public int Number => 1; }");
        var options = AssemblyDecompilationOptions.Default;
        var fingerprint = AssemblyFingerprintCalculator.Create(assemblyPath);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        Assert.DoesNotContain(references.References, reference => !reference.Resolved);
        var cacheKey = AssemblyFingerprintCalculator.CreateCacheKey(fingerprint, options);
        var request = new AssemblyCachePublishRequest(
            fingerprint,
            cacheKey,
            options,
            references,
            new DecompilationResult(
                [new DecompiledDocument(
                    "Value.cs",
                    "Probe.Value",
                    "namespace Probe; public sealed class Value { public int Number => 1; }")],
                [],
                true),
            AssemblySessionStatus.Complete);
        var cache = new AssemblyDecompilationCache(temp.GetPath("cache"));

        Assert.True(cache.Publish(request).Succeeded);
        Assert.Empty(Directory.EnumerateDirectories(temp.GetPath("cache"), "*.tmp", SearchOption.AllDirectories));
        using var barrier = new Barrier(2);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                return cache.Publish(request);
            })));

        Assert.All(results, result =>
        {
            Assert.True(result.Succeeded);
            Assert.NotNull(result.EntryDirectory);
            Assert.True(Directory.Exists(result.EntryDirectory));
        });
    }

    [Fact]
    public async Task AssemblyDecompilationCache_DifferentFingerprintsKeepDelayedPublishResultUntilReturn()
    {
        using var temp = TestTempDirectory.Create("assembly-cache-delayed-return-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CacheDelayedReturnProbe",
            "namespace Probe; public sealed class Value { public int Number => 1; }");
        var options = AssemblyDecompilationOptions.Default;
        var originalFingerprint = AssemblyFingerprintCalculator.Create(assemblyPath);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        Assert.DoesNotContain(references.References, reference => !reference.Resolved);
        var cacheKey = AssemblyFingerprintCalculator.CreateCacheKey(originalFingerprint, options);
        var requests = new[]
        {
            CreatePublishRequest(originalFingerprint with { Sha256 = new string('a', 64) }, cacheKey, options, references, 1),
            CreatePublishRequest(originalFingerprint with { Sha256 = new string('b', 64) }, cacheKey, options, references, 2),
            CreatePublishRequest(originalFingerprint with { Sha256 = new string('c', 64) }, cacheKey, options, references, 3),
        };
        using var delayedReturn = new ManualResetEventSlim(false);
        var firstReturnReached = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReturn = 0;
        var cache = new AssemblyDecompilationCache(
            temp.GetPath("cache"),
            generationDirectory =>
            {
                if (Interlocked.Exchange(ref firstReturn, 1) != 0) return;
                firstReturnReached.SetResult(generationDirectory);
                delayedReturn.Wait();
            });

        var firstTask = Task.Run(() => cache.Publish(requests[0]));
        var firstGeneration = await firstReturnReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(Directory.Exists(firstGeneration));

        var secondTask = Task.Run(() => cache.Publish(requests[1]));
        var thirdTask = Task.Run(() => cache.Publish(requests[2]));
        var competingPublishes = Task.WhenAll(secondTask, thirdTask);

        try
        {
            var completed = await Task.WhenAny(competingPublishes, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.NotSame(competingPublishes, completed);
            Assert.True(Directory.Exists(firstGeneration));
        }
        finally
        {
            delayedReturn.Set();
        }

        var results = await Task.WhenAll(firstTask, secondTask, thirdTask);
        Assert.Equal(3, results.Count(result => result.Succeeded));
    }

    private static AssemblyCachePublishRequest CreatePublishRequest(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey cacheKey,
        AssemblyDecompilationOptions options,
        AssemblyReferenceResolution references,
        int number) =>
        new(
            fingerprint,
            cacheKey,
            options,
            references,
            new DecompilationResult(
                [new DecompiledDocument(
                    "Value.cs",
                    "Probe.Value",
                    $"namespace Probe; public sealed class Value {{ public int Number => {number}; }}")],
                [],
                true),
            AssemblySessionStatus.Complete);
}
