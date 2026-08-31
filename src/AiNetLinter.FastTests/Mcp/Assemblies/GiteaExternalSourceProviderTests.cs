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

[Trait("Category", "Component")]
public sealed class GiteaExternalSourceProviderTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task ResolveAsync_Success_TransfersCheckoutOwnershipAndIdentity()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-success-");
        using var cancellation = new CancellationTokenSource();
        var mapping = CreateMapping("BaselineMini.slnx", "HTTPS://GITEA.EXAMPLE/shared.git");
        var acquirer = CreateAcquirer(staging, fixture, out var transport);
        var materializer = new RecordingMaterializer((requestMapping, checkout, _) =>
            ValueTask.FromResult(ExternalSourceSnapshotTestFactory.CreateSnapshot(
                staging.DirectoryPath,
                requestMapping,
                checkout.LoadedRevision,
                checkout,
                new ExternalSourceProjectSpec(
                    "BaselineMini",
                    "BaselineMini",
                    "public sealed class FixtureType { }"))));
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(mapping, cancellation.Token);

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.None, result.FailureKind);
        var snapshot = Assert.IsType<ExternalSourceSnapshot>(result.SourceSnapshot);
        Assert.Equal("https://gitea.example/shared.git", snapshot.Identity.RepositoryUrl);
        Assert.Equal(Revision, snapshot.Identity.LoadedRevision);
        Assert.Equal("BaselineMini.slnx", snapshot.Identity.SolutionPath);
        Assert.Equal(
            SourceSnapshotIdentity.Create(mapping, Revision),
            snapshot.Identity);
        Assert.Same(mapping, transport.Mapping);
        Assert.Equal(cancellation.Token, transport.CancellationToken);
        Assert.NotNull(materializer.Checkout);
        Assert.False(materializer.Checkout!.IsDisposed);
        Assert.True(Directory.Exists(materializer.Checkout.CheckoutPath));

        using var registry = new SourceSnapshotRegistry();
        using var lease = registry.Acquire(snapshot);
        Assert.Equal(1, registry.ResidentCount);
        registry.Dispose();
        Assert.False(snapshot.IsDisposed);
        Assert.Equal(1, registry.ResidentCount);

        lease.Dispose();
        Assert.True(snapshot.IsDisposed);
        snapshot.Dispose();
        snapshot.Dispose();

        Assert.True(materializer.Checkout.IsDisposed);
        Assert.Equal(0, registry.ResidentCount);
        Assert.False(Directory.Exists(materializer.Checkout.CheckoutPath));
    }

    [Fact]
    public async Task ResolveAsync_AcquisitionFailure_PreservesTypedFailureAndSkipsMaterialization()
    {
        using var staging = TestTempDirectory.Create("external-source-provider-acquisition-failure-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [new(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable,
                    "unsafe diagnostic",
                    "error",
                    "$repository")],
                state: ExternalSourceRepositoryResultState.Create(
                    ExternalSourceProviderFailureKind.ProviderUnavailable));
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var materializer = new RecordingMaterializer((_, _, _) =>
            throw new InvalidOperationException("must not be called"));
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.ProviderUnavailable, result.FailureKind);
        Assert.Null(result.SourceSnapshot);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code
                == ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable);
        Assert.Equal(0, materializer.CallCount);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task ResolveAsync_MaterializationFailure_ReturnsInvalidResponseAndCleansCheckout()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-materialization-failure-");
        var acquirer = CreateAcquirer(staging, fixture, out var transport);
        var materializer = new RecordingMaterializer(FailMaterialization);
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Null(result.SourceSnapshot);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code
                == ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task ResolveAsync_KnownMaterializationFailureSurfacesSafeReason()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-safe-materialization-reason-");
        var acquirer = CreateAcquirer(staging, fixture, out var transport);
        var materializer = new RecordingMaterializer((_, _, _) =>
            throw new ExternalSourceSnapshotMaterializationException(
                ExternalSourceCheckoutTrust.Clean,
                ExternalSourceSnapshotMaterializer.EmptySolutionFailureReason));
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Message.Contains("keine Projekte", StringComparison.Ordinal));
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task ResolveAsync_CancellationFromMaterializer_RethrowsAndCleansCheckout()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-cancellation-");
        using var cancellation = new CancellationTokenSource();
        var acquirer = CreateAcquirer(staging, fixture, out var transport);
        var materializer = new RecordingMaterializer(CancelMaterialization);
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.ResolveAsync(CreateMapping(), cancellation.Token).AsTask());

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task ResolveAsync_CancellationAfterAcquisitionCleansReturnedCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-provider-acquired-cancellation-");
        using var cancellation = new CancellationTokenSource();
        var checkoutPath = staging.CreateSubdirectory("checkout");
        const string ownershipToken = "provider-boundary-test";
        staging.CreateFile(
            "checkout/" + ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
            ownershipToken);
        var ownership = new ExternalSourceCheckoutOwnership(
            staging.DirectoryPath,
            checkoutPath,
            ownershipToken);
        using var checkout = new ExternalSourceCheckoutHandle(
            ownership,
            Path.Combine(checkoutPath, "BaselineMini.slnx"),
            Revision,
            ExternalSourceCheckoutAttestation.ForTesting(checkoutPath, Revision));
        var acquisition = ExternalSourceRepositoryAcquisitionResult.Success(checkout, []);
        var acquirer = new CancellationAfterAcquisitionAcquirer(acquisition, cancellation);
        var provider = new GiteaExternalSourceProvider(
            acquirer,
            new RecordingMaterializer((_, _, _) =>
                throw new InvalidOperationException("must not be materialized")));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.ResolveAsync(CreateMapping(), cancellation.Token).AsTask());

        Assert.True(checkout.IsDisposed);
        Assert.False(Directory.Exists(checkoutPath));
    }

    [Fact]
    public async Task ResolveAsync_SnapshotWithoutCheckoutOwner_IsRejectedAndCleaned()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-provider-owner-failure-");
        var mapping = CreateMapping();
        var acquirer = CreateAcquirer(staging, fixture, out var transport);
        var materializer = new RecordingMaterializer((requestMapping, _, _) =>
            ValueTask.FromResult(ExternalSourceSnapshotTestFactory.CreateSnapshot(
                staging.DirectoryPath,
                requestMapping,
                Revision,
                null,
                new ExternalSourceProjectSpec(
                    "BaselineMini",
                    "BaselineMini",
                    "public sealed class FixtureType { }"))));
        var provider = new GiteaExternalSourceProvider(acquirer, materializer);

        var result = await provider.ResolveAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Null(result.SourceSnapshot);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    private static ExternalSourceRepositoryAcquirer CreateAcquirer(
        TestTempDirectory staging,
        IsolatedFixtureLease fixture,
        out ExternalSourceRecordingTransport transport)
    {
        transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            return ExternalSourceRepositoryTestTransportResults.Success(destination, Revision);
        });
        return ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
    }

    private static ExternalSourceMapping CreateMapping(
        string solutionPath = "BaselineMini.slnx",
        string repositoryUrl = "https://gitea.example/shared.git") =>
        new(
            repositoryUrl,
            solutionPath,
            ["BaselineMini"]);

    private static ValueTask<ExternalSourceSnapshot> FailMaterialization(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("secret materializer detail");

    private static ValueTask<ExternalSourceSnapshot> CancelMaterialization(
        ExternalSourceMapping mapping,
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken) =>
        throw new OperationCanceledException(cancellationToken);

    private sealed class RecordingMaterializer : IExternalSourceSnapshotMaterializer
    {
        private readonly Func<
            ExternalSourceMapping,
            ExternalSourceCheckoutHandle,
            CancellationToken,
            ValueTask<ExternalSourceSnapshot>> operation;

        internal RecordingMaterializer(
            Func<
                ExternalSourceMapping,
                ExternalSourceCheckoutHandle,
                CancellationToken,
                ValueTask<ExternalSourceSnapshot>> operation)
        {
            this.operation = operation;
        }

        internal int CallCount { get; private set; }

        internal ExternalSourceCheckoutHandle? Checkout { get; private set; }

        public ValueTask<ExternalSourceSnapshot> MaterializeAsync(
            ExternalSourceMapping mapping,
            ExternalSourceCheckoutHandle checkout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Checkout = checkout;
            return operation(mapping, checkout, cancellationToken);
        }
    }

    private sealed class CancellationAfterAcquisitionAcquirer(
        ExternalSourceRepositoryAcquisitionResult acquisition,
        CancellationTokenSource cancellation) : IExternalSourceRepositoryAcquirer
    {
        public Task<ExternalSourceRepositoryAcquisitionResult> AcquireAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromResult(acquisition);
        }
    }
}
