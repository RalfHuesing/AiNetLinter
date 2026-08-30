#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers ExternalSourceCheckoutAttestation
[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryCheckoutAttestationTests
{
    [Fact]
    public async Task CachePublish_MutationBeforePointerPublishFailsClosed()
    {
        using var source = SourceFixture.Create(ExternalSourceRepositoryCacheTestData.Revision);
        using var cache = TestTempDirectory.Create("external-source-attestation-race-cache-");
        var sourceFile = Path.Combine(source.CheckoutPath, "src", "Program.cs");
        var attestation = ExternalSourceCheckoutAttestation.ForTesting(
            source.CheckoutPath,
            ExternalSourceRepositoryCacheTestData.Revision,
            (_, _) => new ValueTask<ExternalSourceCheckoutVerification>(
                string.Equals(File.ReadAllText(sourceFile), "class Program { }", StringComparison.Ordinal)
                    ? ExternalSourceCheckoutVerification.Clean
                    : ExternalSourceCheckoutVerification.Unverified));
        using var handle = new ExternalSourceCheckoutHandle(
            source.Handle.Ownership,
            source.Handle.SolutionPath,
            ExternalSourceRepositoryCacheTestData.Revision,
            attestation);
        var request = new ExternalSourceRepositoryCachePublishRequest
        {
            Mapping = source.Request.Mapping,
            Checkout = handle,
            CheckoutOwnership = source.Request.CheckoutOwnership,
            CacheKey = source.Request.CacheKey,
            SolutionPath = source.Request.SolutionPath,
            LoadedRevision = source.Request.LoadedRevision,
        };
        var writer = new LocalExternalSourceRepositoryCacheWriter(cache.DirectoryPath);

        var result = await writer.PublishAsync(
            request,
            CancellationToken.None,
            testSeam: new ExternalSourceRepositoryCachePublishTestSeam
            {
                BeforePointerPublishedAsync = () =>
                {
                    File.WriteAllText(sourceFile, "class Program { static int Changed => 1; }");
                    return Task.CompletedTask;
                },
            });

        Assert.False(result.Succeeded);
        Assert.Equal(ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource, result.FailureKind);
        Assert.Equal(ExternalSourceCheckoutTrust.Unverified, result.CheckoutTrust);
        Assert.False(writer.TryReadCurrent(source.Key, out _, out _));
        Assert.True(File.Exists(sourceFile));
    }

    [Fact]
    public async Task Provider_MutationAfterMaterializationFailsClosedWithoutSnapshot()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-attestation-race-");
        var mapping = new ExternalSourceMapping(
            ExternalSourceRepositoryCacheTestData.RepositoryUrl,
            "BaselineMini.slnx",
            ["BaselineMini"]);
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            var sourceFile = Path.Combine(destination, "Program.cs");
            File.WriteAllText(sourceFile, "clean");
            return ExternalSourceRepositoryTransportResult.Success(
                ExternalSourceRepositoryCacheTestData.Revision,
                ExternalSourceCheckoutAttestation.ForTesting(
                    destination,
                    ExternalSourceRepositoryCacheTestData.Revision,
                    (_, _) => new ValueTask<ExternalSourceCheckoutVerification>(
                        string.Equals(File.ReadAllText(sourceFile), "clean", StringComparison.Ordinal)
                            ? ExternalSourceCheckoutVerification.Clean
                            : ExternalSourceCheckoutVerification.Unverified)));
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var materializer = new MutatingMaterializer();
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Equal(ExternalSourceCheckoutTrust.Unverified, result.CheckoutTrust);
        Assert.Null(result.SourceSnapshot);
        Assert.Equal(1, materializer.CallCount);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task DirtyTransportTrustIsPreservedThroughAcquirerAndProvider()
    {
        using var staging = TestTempDirectory.Create("external-source-dirty-trust-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "ignored.txt"), "ignored");
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutDirty,
                    "dirty", "test", "$repository")],
                checkoutTrust: ExternalSourceCheckoutTrust.Dirty,
                state: ExternalSourceRepositoryResultState.Create(
                    ExternalSourceProviderFailureKind.InvalidResponse));
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var acquisition = await acquirer.AcquireAsync(
            ExternalSourceRepositoryCacheTestData.CreateMapping());
        var provider = new GiteaExternalSourceProvider(
            acquirer,
            new MutatingMaterializer());
        var providerResult = await provider.ResolveAsync(
            ExternalSourceRepositoryCacheTestData.CreateMapping());

        Assert.False(acquisition.IsAvailable);
        Assert.Equal(ExternalSourceCheckoutTrust.Dirty, acquisition.CheckoutTrust);
        Assert.Equal(ExternalSourceCheckoutTrust.Dirty, providerResult.CheckoutTrust);
        Assert.Equal(ExternalSourceRepositoryHealth.Unavailable, providerResult.Health);
    }

    private sealed class MutatingMaterializer : IExternalSourceSnapshotMaterializer
    {
        internal int CallCount { get; private set; }

        public ValueTask<ExternalSourceSnapshot> MaterializeAsync(
            ExternalSourceMapping mapping,
            ExternalSourceCheckoutHandle checkout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            File.WriteAllText(Path.Combine(checkout.CheckoutPath, "Program.cs"), "mutated");
            return ValueTask.FromResult(ExternalSourceSnapshotTestFactory.CreateSnapshot(
                checkout.CheckoutPath,
                mapping,
                checkout.LoadedRevision,
                checkout,
                new ExternalSourceProjectSpec(
                    "BaselineMini",
                    "BaselineMini",
                    "public sealed class FixtureType { }")));
        }
    }
}
