#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestAssertions;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestData;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers ExternalSourceRepositoryCacheReuse
[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryCacheReuseTests
{
    [Fact]
    public async Task Acquirer_ValidCacheHitCreatesIndependentCheckoutWithoutTransportOrPublish()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-hit-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-staging-");
        var cachePublisher = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await cachePublisher.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var cacheReader = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var cacheWriter = new RecordingCacheWriter();
        var currentGenerationBefore = ReadCurrentGenerationName(cacheReader, source.Key);
        var transport = new ExternalSourceRecordingTransport((_, _, _) =>
            throw new InvalidOperationException("Der Cache-Hit darf keinen Transport aufrufen."));
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: cacheWriter,
            cacheReader: cacheReader);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(Revision, result.LoadedRevision);
        Assert.Equal(0, transport.CallCount);
        Assert.Null(cacheWriter.Request);
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        AssertRequestOwnedCheckout(checkout, published.GenerationPath);
        checkout.Dispose();
        Assert.False(Directory.Exists(checkout.CheckoutPath));
        Assert.True(Directory.Exists(published.GenerationPath));
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
        Assert.Null(cacheWriter.Request);
    }

    [Fact]
    public async Task CacheReuse_ValidCurrentReturnsRequestOwnedCheckout()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-direct-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-direct-staging-");
        var cachePublisher = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await cachePublisher.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var cacheReader = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var currentGenerationBefore = ReadCurrentGenerationName(cacheReader, source.Key);
        var reuse = new ExternalSourceRepositoryCacheReuse(
            staging.DirectoryPath,
            cacheReader,
            Serilog.Log.Logger);

        var result = reuse.TryAcquire(
            source.Key.CanonicalRepositoryUrl,
            source.Key.SolutionPath,
            CancellationToken.None);
        Assert.NotNull(typeof(ExternalSourceRepositoryCacheReuse));
        var checkoutResult = Assert.IsType<ExternalSourceRepositoryAcquisitionResult>(result);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(checkoutResult.Checkout);
        Assert.Equal(Revision, checkoutResult.LoadedRevision);
        AssertRequestOwnedCheckout(checkout, published.GenerationPath);
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
        checkout.Dispose();
        Assert.False(Directory.Exists(checkout.CheckoutPath));
        Assert.True(Directory.Exists(published.GenerationPath));
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
    }

    [Fact]
    public async Task Acquirer_ConcurrentCacheHitsCreateIndependentLeases()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-concurrent-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-concurrent-staging-");
        var cachePublisher = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await cachePublisher.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var cacheReader = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var cacheWriter = new RecordingCacheWriter();
        var currentGenerationBefore = ReadCurrentGenerationName(cacheReader, source.Key);
        var transport = new ExternalSourceRecordingTransport((_, _, _) =>
            throw new InvalidOperationException("Concurrent Cache-Hits dürfen keinen Transport aufrufen."));
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: cacheWriter,
            cacheReader: cacheReader);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => acquirer.AcquireAsync(CreateMapping())));
        Assert.All(results, result => Assert.True(result.IsAvailable));
        var checkouts = results.Select(result => Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout)).ToArray();
        Assert.Equal(4, checkouts.Select(checkout => checkout.CheckoutPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(checkouts, checkout => AssertRequestOwnedCheckout(checkout, published.GenerationPath));
        Assert.Equal(0, transport.CallCount);
        Assert.Null(cacheWriter.Request);
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
        foreach (var checkout in checkouts)
        {
            checkout.Dispose();
            Assert.False(Directory.Exists(checkout.CheckoutPath));
        }

        Assert.True(Directory.Exists(published.GenerationPath));
        Assert.Empty(Directory.EnumerateDirectories(staging.DirectoryPath, "checkout-*"));
        Assert.Equal(currentGenerationBefore, ReadCurrentGenerationName(cacheReader, source.Key));
        Assert.Null(cacheWriter.Request);
    }
}
