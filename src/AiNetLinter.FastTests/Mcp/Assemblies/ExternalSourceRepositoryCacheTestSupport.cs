#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheTestData
{
    internal const string RepositoryUrl = "https://gitea.example/shared.git";
    internal const string Revision = "0123456789abcdef0123456789abcdef01234567";
    internal const string OtherRevision = "fedcba9876543210fedcba9876543210fedcba98";
    internal const string SolutionPath = "src/BaselineMini.slnx";

    internal static ExternalSourceMapping CreateMapping() =>
        new(RepositoryUrl, SolutionPath, ["BaselineMini"]);
}

internal static class ExternalSourceRepositoryCacheTestAssertions
{
    internal static ExternalSourceRepositoryCacheReadResult? ReadCurrent(
        LocalExternalSourceRepositoryCacheWriter writer,
        ExternalSourceRepositoryCacheKey key)
    {
        Assert.True(writer.TryReadCurrent(key, out var result, out var diagnostic));
        Assert.Null(diagnostic);
        return result;
    }

    internal static string ReadCurrentGenerationName(
        IExternalSourceRepositoryCacheReader cacheReader,
        ExternalSourceRepositoryCacheKey key)
    {
        Assert.True(cacheReader.TryReadCurrent(key, out var current, out var diagnostic));
        Assert.Null(diagnostic);
        return current!.Manifest.GenerationName;
    }

    internal static void AssertRequestOwnedCheckout(
        ExternalSourceCheckoutHandle checkout,
        string? persistentGenerationPath)
    {
        Assert.NotEqual(persistentGenerationPath, checkout.CheckoutPath);
        Assert.True(File.Exists(Path.Combine(
            checkout.CheckoutPath,
            ExternalSourceCheckoutOwnership.OwnershipMarkerFileName)));
        Assert.Equal(
            Path.Combine(
                checkout.CheckoutPath,
                ExternalSourceRepositoryCacheTestData.SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
            checkout.SolutionPath);
    }
}

internal sealed class RecordingCacheWriter : IExternalSourceRepositoryCacheWriter
{
    internal ExternalSourceRepositoryCachePublishRequest? Request { get; private set; }

    internal bool ReturnFailure { get; init; }

    public Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken = default)
    {
        Request = request;
        return Task.FromResult(ReturnFailure
            ? ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.WriteFailed)
            : ExternalSourceRepositoryCachePublishResult.Success(
                request.CacheKey,
                "generation-00000000000000000000000000000000",
                "cache-generation"));
    }
}

internal sealed class FixedCacheReader : IExternalSourceRepositoryCacheReader
{
    private readonly ExternalSourceRepositoryCacheReadResult result;

    internal FixedCacheReader(ExternalSourceRepositoryCacheReadResult result)
    {
        this.result = result;
    }

    public bool TryReadCurrent(
        ExternalSourceRepositoryCacheKey key,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        result = this.result;
        diagnostic = null;
        return true;
    }
}

internal sealed class CancellingCacheReader : IExternalSourceRepositoryCacheReader
{
    private readonly IExternalSourceRepositoryCacheReader inner;
    private readonly CancellationTokenSource cancellation;

    internal CancellingCacheReader(
        IExternalSourceRepositoryCacheReader inner,
        CancellationTokenSource cancellation)
    {
        this.inner = inner;
        this.cancellation = cancellation;
    }

    public bool TryReadCurrent(
        ExternalSourceRepositoryCacheKey key,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        var success = inner.TryReadCurrent(key, out result, out diagnostic);
        cancellation.Cancel();
        return success;
    }
}

internal sealed class SourceFixture : IDisposable
{
    private readonly TestTempDirectory temp;

    private SourceFixture(
        TestTempDirectory temp,
        ExternalSourceCheckoutHandle handle,
        ExternalSourceRepositoryCacheKey key,
        ExternalSourceRepositoryCachePublishRequest request,
        string checkoutPath)
    {
        this.temp = temp;
        Handle = handle;
        Key = key;
        Request = request;
        CheckoutPath = checkoutPath;
    }

    internal ExternalSourceCheckoutHandle Handle { get; }

    internal ExternalSourceRepositoryCacheKey Key { get; }

    internal ExternalSourceRepositoryCachePublishRequest Request { get; }

    internal string CheckoutPath { get; }

    internal static SourceFixture Create(string revision)
    {
        var temp = TestTempDirectory.Create("external-source-cache-fixture-");
        var checkoutPath = temp.CreateSubdirectory("checkout");
        temp.CreateFile("checkout/src/BaselineMini.slnx", "solution");
        temp.CreateFile("checkout/src/Program.cs", "class Program { }");
        temp.CreateFile("checkout/.git/config", "[core]\n\trepositoryformatversion = 0");
        const string markerValue = "cache-test-marker";
        temp.CreateFile(
            "checkout/" + ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
            markerValue);
        var ownership = new ExternalSourceCheckoutOwnership(
            temp.DirectoryPath,
            checkoutPath,
            markerValue);
        var handle = new ExternalSourceCheckoutHandle(
            ownership,
            Path.Combine(checkoutPath, "src", "BaselineMini.slnx"),
            revision);
        Assert.True(ExternalSourceRepositoryCacheKey.TryCreate(
            ExternalSourceRepositoryCacheTestData.RepositoryUrl,
            ExternalSourceRepositoryCacheTestData.SolutionPath,
            out var key));
        var request = new ExternalSourceRepositoryCachePublishRequest
        {
            Mapping = ExternalSourceRepositoryCacheTestData.CreateMapping(),
            Checkout = handle,
            CheckoutOwnership = ownership,
            CacheKey = key!,
            SolutionPath = ExternalSourceRepositoryCacheTestData.SolutionPath,
            LoadedRevision = revision,
        };
        return new(temp, handle, key!, request, checkoutPath);
    }

    public void Dispose()
    {
        Handle.Dispose();
        temp.Dispose();
    }
}
