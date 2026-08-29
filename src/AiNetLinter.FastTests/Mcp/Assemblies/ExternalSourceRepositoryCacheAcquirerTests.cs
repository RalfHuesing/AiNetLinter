#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestAssertions;
using static AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRepositoryCacheTestData;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

public sealed partial class ExternalSourceRepositoryCacheWriterTests
{
    [Fact]
    public async Task Acquirer_WriteThroughUsesHandleWithoutTransferringOwnership()
    {
        using var staging = TestTempDirectory.Create("external-source-cache-acquirer-");
        var writer = new RecordingCacheWriter();
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        var request = Assert.IsType<ExternalSourceRepositoryCachePublishRequest>(writer.Request);
        Assert.Same(checkout, request.Checkout);
        Assert.NotNull(request.CheckoutOwnership);
        Assert.Equal(Revision, request.LoadedRevision);
        Assert.False(checkout.IsDisposed);
        checkout.Dispose();
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task Acquirer_CachePublishFailureStaysVisibleWithoutRemovingSuccess()
    {
        using var staging = TestTempDirectory.Create("external-source-cache-acquirer-failure-");
        var writer = new RecordingCacheWriter { ReturnFailure = true };
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.None, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        checkout.Dispose();
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task Acquirer_DoesNotPublishCacheForTransportFailure()
    {
        using var staging = TestTempDirectory.Create("external-source-cache-acquirer-no-publish-");
        var writer = new RecordingCacheWriter();
        var transport = new ExternalSourceRecordingTransport((_, _, _) =>
            new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: Array.Empty<ExternalSourceConfigurationDiagnostic>(),
                failureKind: ExternalSourceProviderFailureKind.NetworkUnavailable));
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Null(writer.Request);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("solution")]
    [InlineData("revision")]
    [InlineData("inventory")]
    public async Task Acquirer_InvalidCacheIdentityFallsBackToClone(string mutation)
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-invalid-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-invalid-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        MutateCache(mutation, published.GenerationPath!, source.Key);

        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer,
            cacheReader: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(1, transport.CallCount);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        checkout.Dispose();
        Assert.False(Directory.Exists(checkout.CheckoutPath));
    }

    [Fact]
    public async Task Acquirer_MissingCurrentFallsBackToClone()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-missing-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-missing-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        File.Delete(Path.Combine(
            writer.GetEntryDirectory(source.Key),
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName));

        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer,
            cacheReader: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(1, transport.CallCount);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        checkout.Dispose();
    }

    [Theory]
    [InlineData("pointer")]
    [InlineData("manifest")]
    [InlineData("inventory")]
    [InlineData("content")]
    public async Task Acquirer_MissingCacheArtifactFallsBackToClone(string artifact)
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-missing-artifact-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-missing-artifact-staging-");
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await writer.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var generationPath = published.GenerationPath!;
        var artifactPath = artifact switch
        {
            "pointer" => Path.Combine(
                writer.GetEntryDirectory(source.Key),
                ExternalSourceRepositoryCacheContract.CurrentPointerFileName),
            "manifest" => Path.Combine(
                generationPath,
                ExternalSourceRepositoryCacheContract.ManifestFileName),
            "inventory" => Path.Combine(
                generationPath,
                ExternalSourceRepositoryCacheContract.InventoryFileName),
            "content" => Path.Combine(
                generationPath,
                ExternalSourceRepositoryCacheContract.ContentDirectoryName,
                source.Request.SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
            _ => throw new ArgumentException("Unbekanntes Cache-Artefakt.", nameof(artifact)),
        };
        File.Delete(artifactPath);

        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer,
            cacheReader: writer);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(1, transport.CallCount);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        checkout.Dispose();
        Assert.True(Directory.Exists(generationPath));
    }

    [Fact]
    public async Task Acquirer_MaterializationFailureCleansLeaseAndFallsBack()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-materialize-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-materialize-staging-");
        var localReader = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        var published = await localReader.PublishAsync(source.Request);
        Assert.True(published.Succeeded);
        var read = Assert.IsType<ExternalSourceRepositoryCacheReadResult>(
            ReadCurrent(localReader, source.Key));
        var tamperedManifest = new ExternalSourceRepositoryCacheManifest(
            read.Manifest.CacheSchemaVersion,
            read.Manifest.CacheKey,
            read.Manifest.CanonicalRepositoryUrl,
            read.Manifest.SolutionPath,
            read.Manifest.LoadedRevision,
            read.Manifest.GenerationName,
            read.Manifest.CreatedUtc,
            read.Manifest.Files.Select((file, index) => index == 0
                ? file with { ContentHash = new string('0', file.ContentHash.Length) }
                : file).ToArray());
        var reader = new FixedCacheReader(new(
            tamperedManifest,
            read.GenerationPath));
        var writer = new RecordingCacheWriter();
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(Path.Combine(destination, "src"));
            File.WriteAllText(
                Path.Combine(destination, SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
                "solution");
            return ExternalSourceRepositoryTransportResult.Success(Revision);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: writer,
            cacheReader: reader);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.True(result.IsAvailable);
        Assert.Equal(1, transport.CallCount);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        checkout.Dispose();
        Assert.False(Directory.Exists(checkout.CheckoutPath));
        Assert.True(localReader.TryReadCurrent(source.Key, out var current, out var diagnostic));
        Assert.Null(diagnostic);
        Assert.Equal(read.Manifest.GenerationName, current!.Manifest.GenerationName);
    }

    [Fact]
    public async Task Acquirer_CacheHitCancellationRethrowsWithoutClone()
    {
        using var source = SourceFixture.Create(Revision);
        using var cache = TestTempDirectory.Create("external-source-cache-reuse-cancel-");
        using var staging = TestTempDirectory.Create("external-source-cache-reuse-cancel-staging-");
        var localReader = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);
        Assert.True((await localReader.PublishAsync(source.Request)).Succeeded);
        using var cancellation = new CancellationTokenSource();
        var reader = new CancellingCacheReader(localReader, cancellation);
        var transport = new ExternalSourceRecordingTransport((_, _, _) =>
            throw new InvalidOperationException("Cancellation darf nicht in Clone umgedeutet werden."));
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            cacheWriter: localReader,
            cacheReader: reader);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            acquirer.AcquireAsync(CreateMapping(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, transport.CallCount);
        Assert.Empty(Directory.EnumerateDirectories(staging.DirectoryPath, "checkout-*"));
    }

    private static void MutateCache(
        string mutation,
        string generationPath,
        ExternalSourceRepositoryCacheKey key)
    {
        var manifestPath = Path.Combine(
            generationPath,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var inventoryPath = Path.Combine(
            generationPath,
            ExternalSourceRepositoryCacheContract.InventoryFileName);
        var value = File.ReadAllText(mutation == "inventory" ? inventoryPath : manifestPath);
        value = mutation switch
        {
            "url" => value.Replace(
                "https://gitea.example/shared.git",
                "https://gitea.example/other.git",
                StringComparison.Ordinal),
            "solution" => value.Replace(SolutionPath, "src/Other.slnx", StringComparison.Ordinal),
            "revision" => value.Replace(Revision, "invalid-revision", StringComparison.Ordinal),
            "inventory" => value.Replace(key.StableValue, new string('a', 64), StringComparison.Ordinal),
            _ => throw new ArgumentException("Unbekannte Cache-Mutation.", nameof(mutation)),
        };
        File.WriteAllText(mutation == "inventory" ? inventoryPath : manifestPath, value);
    }

}
