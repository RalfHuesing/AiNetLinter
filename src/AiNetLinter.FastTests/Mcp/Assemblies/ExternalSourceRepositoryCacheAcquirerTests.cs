#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

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
}
