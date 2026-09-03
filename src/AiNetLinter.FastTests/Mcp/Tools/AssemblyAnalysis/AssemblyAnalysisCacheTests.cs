#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        Assert.True((await cache.PublishAsync(request)).Succeeded);
        Assert.Empty(Directory.EnumerateDirectories(temp.GetPath("cache"), "*.tmp", SearchOption.AllDirectories));
        using var barrier = new Barrier(2);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await cache.PublishAsync(request);
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

        var firstTask = Task.Run(async () => await cache.PublishAsync(requests[0]));
        var firstGeneration = await firstReturnReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(Directory.Exists(firstGeneration));

        var secondTask = Task.Run(async () => await cache.PublishAsync(requests[1]));
        var thirdTask = Task.Run(async () => await cache.PublishAsync(requests[2]));
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

    [Fact]
    public async Task AssemblyDecompilationCache_RejectsIncompleteDecompilationBeforePublishing()
    {
        using var temp = TestTempDirectory.Create("assembly-cache-incomplete-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CacheIncompleteProbe",
            "namespace Probe; public sealed class Value { public int Number => 1; }");
        var options = AssemblyDecompilationOptions.Default;
        var fingerprint = AssemblyFingerprintCalculator.Create(assemblyPath);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        var cacheKey = AssemblyFingerprintCalculator.CreateCacheKey(fingerprint, options);
        var request = CreatePublishRequest(fingerprint, cacheKey, options, references, 1, isComplete: false);
        var cache = new AssemblyDecompilationCache(temp.GetPath("cache"));

        var result = await cache.PublishAsync(request);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(Directory.Exists(cache.RootPath)
            ? Directory.EnumerateFiles(cache.RootPath, "current.json", SearchOption.AllDirectories)
            : []);
        Assert.Empty(Directory.Exists(cache.RootPath)
            ? Directory.EnumerateDirectories(cache.RootPath, "*.tmp", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task AssemblyDecompilationCache_DoesNotUseIncompleteGenerationAsCacheHit()
    {
        using var temp = TestTempDirectory.Create("assembly-cache-incomplete-hit-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CacheIncompleteHitProbe",
            "namespace Probe; public sealed class Value { public int Number => 1; }");
        var options = AssemblyDecompilationOptions.Default;
        var fingerprint = AssemblyFingerprintCalculator.Create(assemblyPath);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        var cacheKey = AssemblyFingerprintCalculator.CreateCacheKey(fingerprint, options);
        var request = CreatePublishRequest(fingerprint, cacheKey, options, references, 1);
        var cache = new AssemblyDecompilationCache(temp.GetPath("cache"));
        Assert.True((await cache.PublishAsync(request)).Succeeded);

        var entryDirectory = cache.GetEntryDirectory(cacheKey);
        var pointerPath = Path.Combine(entryDirectory, AssemblyCacheContract.CurrentPointerFileName);
        using var pointer = JsonDocument.Parse(File.ReadAllText(pointerPath));
        var generationDirectory = Path.Combine(entryDirectory, pointer.RootElement.GetProperty("generation").GetString()!);
        var manifestPath = Path.Combine(generationDirectory, AssemblyCacheContract.ManifestFileName);
        var incompleteManifest = File.ReadAllText(manifestPath)
            .Replace("\"errors\": []", "\"errors\": [\"incomplete decompilation\"]", StringComparison.Ordinal)
            .Replace("\"status\": \"complete\"", "\"status\": \"partial\"", StringComparison.Ordinal)
            .Replace("\"complete\": true", "\"complete\": false", StringComparison.Ordinal);
        File.WriteAllText(manifestPath, incompleteManifest);

        var canRead = cache.TryRead(
            new AssemblyCacheReadRequest(cacheKey, fingerprint, references),
            out var generation,
            out var diagnostic);

        Assert.False(canRead);
        Assert.Null(generation);
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public async Task AssemblyDecompilationCache_KeepsPointerGenerationWhenValidationIsLocked()
    {
        using var temp = TestTempDirectory.Create("assembly-cache-pointer-lock-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "CachePointerLockProbe",
            "namespace Probe; public sealed class Value { public int Number => 1; }");
        var options = AssemblyDecompilationOptions.Default;
        var fingerprint = AssemblyFingerprintCalculator.Create(assemblyPath);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        var cacheKey = AssemblyFingerprintCalculator.CreateCacheKey(fingerprint, options);
        var initialRequest = CreatePublishRequest(fingerprint, cacheKey, options, references, 1);
        var cacheRoot = temp.GetPath("cache");
        var cache = new AssemblyDecompilationCache(cacheRoot);
        Assert.True((await cache.PublishAsync(initialRequest)).Succeeded);

        var changedRequest = CreatePublishRequest(
            fingerprint with { Sha256 = new string('b', 64) },
            cacheKey,
            options,
            references,
            2);
        FileStream? heldPointer = null;
        string? publishedGenerationDirectory = null;
        var lockAcquired = 0;
        var lockedCache = new AssemblyDecompilationCache(
            cacheRoot,
            beforePointerValidation: generationDirectory =>
            {
                if (Interlocked.Exchange(ref lockAcquired, 1) != 0) return;
                publishedGenerationDirectory = generationDirectory;
                heldPointer = new FileStream(
                    Path.Combine(Path.GetDirectoryName(generationDirectory)!, AssemblyCacheContract.CurrentPointerFileName),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
            });

        AssemblyCachePublishResult result;
        try
        {
            result = await lockedCache.PublishAsync(changedRequest);

            Assert.False(result.Succeeded);
            Assert.NotNull(publishedGenerationDirectory);
            Assert.True(Directory.Exists(publishedGenerationDirectory));
        }
        finally
        {
            heldPointer?.Dispose();
        }

        Assert.True(lockedCache.TryRead(
            new AssemblyCacheReadRequest(cacheKey, changedRequest.Fingerprint, references),
            out var generation,
            out var diagnostic));
        Assert.NotNull(generation);
        Assert.Null(diagnostic);
    }

    private static AssemblyCachePublishRequest CreatePublishRequest(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationCacheKey cacheKey,
        AssemblyDecompilationOptions options,
        AssemblyReferenceResolution references,
        int number,
        bool isComplete = true) =>
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
                isComplete),
            AssemblySessionStatus.Complete);
}
