#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryAcquirerTests
{
    [Fact]
    public async Task AcquireAsync_Success_ReturnsVerifiedCheckoutAndHandleOwnsCleanup()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-acquirer-success-");
        var foreignFile = staging.CreateFile("foreign/keep.txt", "keep");
        var mapping = CreateMapping();
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var result = await acquirer.AcquireAsync(mapping);

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.None, result.FailureKind);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        Assert.Equal("revision-42", result.LoadedRevision);
        Assert.Equal("revision-42", checkout.LoadedRevision);
        Assert.True(File.Exists(checkout.SolutionPath));
        Assert.Equal(Path.Combine(checkout.CheckoutPath, "BaselineMini.slnx"), checkout.SolutionPath);
        Assert.StartsWith(staging.DirectoryPath + Path.DirectorySeparatorChar, checkout.CheckoutPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(transport.DestinationHadNoWorkingTreeEntriesAtCall);
        Assert.Same(mapping, transport.Mapping);

        checkout.Dispose();
        checkout.Dispose();

        Assert.True(File.Exists(foreignFile));
        Assert.False(Directory.Exists(checkout.CheckoutPath));
        Assert.True(checkout.IsDisposed);
        Assert.Equal(ExternalSourceCheckoutCleanupState.Succeeded, checkout.CleanupState);
    }

    [Theory]
    [InlineData((int)ExternalSourceProviderFailureKind.ProviderUnavailable)]
    [InlineData((int)ExternalSourceProviderFailureKind.AuthenticationRequired)]
    [InlineData((int)ExternalSourceProviderFailureKind.AccessDenied)]
    [InlineData((int)ExternalSourceProviderFailureKind.RepositoryNotFound)]
    [InlineData((int)ExternalSourceProviderFailureKind.NetworkUnavailable)]
    [InlineData((int)ExternalSourceProviderFailureKind.Timeout)]
    [InlineData((int)ExternalSourceProviderFailureKind.InvalidResponse)]
    public async Task AcquireAsync_TransportFailure_PreservesTypedFailureAndCleansOwnCheckout(
        int failureKindValue)
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-failure-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [Diagnostic("transport-warning")],
                failureKind: (ExternalSourceProviderFailureKind)failureKindValue);
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal((ExternalSourceProviderFailureKind)failureKindValue, result.FailureKind);
        Assert.Null(result.Checkout);
        Assert.Null(result.LoadedRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed);
        Assert.All(result.Diagnostics, AssertSafeTransportDiagnostic);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task AcquireAsync_Cancellation_RethrowsAndCleansOwnCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-cancel-");
        using var cancellation = new CancellationTokenSource();
        var transport = new ExternalSourceRecordingTransport((_, destination, token) =>
        {
            Directory.CreateDirectory(destination);
            cancellation.Cancel();
            throw new OperationCanceledException(token);
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            acquirer.AcquireAsync(CreateMapping(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(Directory.Exists(transport.DestinationPath));
        Assert.Equal(cancellation.Token, transport.CancellationToken);
    }

    [Fact]
    public async Task AcquireAsync_TransportError_ReturnsTypedFailureAndCleansOwnCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-error-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            throw new InvalidOperationException("secret transport detail");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
        Assert.False(Directory.Exists(transport.DestinationPath));
    }
    [Fact]
    public async Task AcquireAsync_HttpTransportException_MapsToNetworkFailureAndCleansOwnCheckout()
    {
        await AssertTransportExceptionMapsAsync(
            new HttpRequestException("https://user:secret@example.test/repository"),
            ExternalSourceProviderFailureKind.NetworkUnavailable);
    }
    [Fact]
    public async Task AcquireAsync_TimeoutTransportException_MapsToTimeoutFailureAndCleansOwnCheckout()
    {
        await AssertTransportExceptionMapsAsync(
            new TimeoutException("Bearer secret-token"),
            ExternalSourceProviderFailureKind.Timeout);
    }
    [Fact]
    public async Task AcquireAsync_AccessTransportException_MapsToAccessDeniedAndCleansOwnCheckout()
    {
        await AssertTransportExceptionMapsAsync(
            new UnauthorizedAccessException("password=secret"),
            ExternalSourceProviderFailureKind.AccessDenied);
    }
    [Fact]
    public async Task AcquireAsync_UnknownTransportException_MapsToInvalidResponseAndCleansOwnCheckout()
    {
        await AssertTransportExceptionMapsAsync(
            new InvalidDataException("exception detail with token=secret"),
            ExternalSourceProviderFailureKind.InvalidResponse);
    }
    [Fact]
    public async Task AcquireAsync_PrivilegeNotHeld_MapsToRepositoryCapabilityFailure()
    {
        await AssertTransportExceptionMapsAsync(
            new Win32Exception(ExternalSourceRepositoryFailurePolicy.ErrorPrivilegeNotHeld),
            ExternalSourceProviderFailureKind.ProviderUnavailable,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable);
    }
    [Fact]
    public async Task AcquireAsync_CancellationAfterTransportSuccess_RethrowsAndCleansOwnCheckout()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-acquirer-cancel-after-success-");
        using var cancellation = new CancellationTokenSource();
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            cancellation.Cancel();
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            acquirer.AcquireAsync(CreateMapping(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }
    [Fact]
    public void TransportResult_RedactsUntrustedDiagnosticsToSafeContract()
    {
        const string secret = "https://user:password@example.test/repository Bearer token-value";
        var result = new ExternalSourceRepositoryTransportResult(
            isAvailable: false,
            loadedRevision: null,
            diagnostics: [new(
                "diagnostic-code=" + secret,
                "exception detail " + secret,
                "warning",
                secret)],
            failureKind: ExternalSourceProviderFailureKind.InvalidResponse);

        Assert.Single(result.Diagnostics);
        Assert.Equal(
            ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed,
            result.Diagnostics[0].Code);
        AssertSafeTransportDiagnostic(result.Diagnostics[0]);
        Assert.DoesNotContain(secret, result.Diagnostics[0].Code, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, result.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, result.Diagnostics[0].Location, StringComparison.Ordinal);
    }
    [Fact]
    public async Task AcquireAsync_TransportReplacesCheckout_DoesNotDeleteForeignTree()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-replacement-");
        var movedCheckout = Path.Combine(staging.DirectoryPath, "foreign-checkout");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.Move(destination, movedCheckout);
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "foreign.txt"), "must remain");
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
        Assert.True(Directory.Exists(movedCheckout));
        Assert.Equal("must remain", File.ReadAllText(Path.Combine(transport.DestinationPath!, "foreign.txt")));
    }
    [Fact]
    public async Task AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains()
    {
        WindowsReparseCapabilityGate.Require();
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-acquirer-reparse-");
        var sentinelDirectory = staging.CreateSubdirectory("external-sentinel");
        var sentinel = staging.CreateFile("external-sentinel/keep.txt", "keep");
        var linkPath = Path.Combine("target", "external-link");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            Directory.CreateDirectory(Path.Combine(destination, "target"));
            Directory.CreateSymbolicLink(Path.Combine(destination, linkPath), sentinelDirectory);
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.ProviderUnavailable, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable);
        Assert.True(File.Exists(sentinel));
        Assert.Equal("keep", File.ReadAllText(sentinel));
        Assert.False(Directory.Exists(Path.Combine(transport.DestinationPath!, linkPath)));
    }

    [Fact]
    public async Task CheckoutHandle_DisposeReportsLostOwnershipAndRemainsIdempotent()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-acquirer-cleanup-state-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(
                fixture.RootPath,
                destination);
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var result = await acquirer.AcquireAsync(CreateMapping());
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);

        File.Delete(Path.Combine(
            checkout.CheckoutPath,
            ExternalSourceCheckoutOwnership.OwnershipMarkerFileName));
        checkout.Dispose();

        Assert.Equal(
            ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed,
            checkout.CleanupState);
        Assert.True(Directory.Exists(checkout.CheckoutPath));
        checkout.Dispose();
        Assert.Equal(
            ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed,
            checkout.CleanupState);
    }

    private async Task AssertTransportExceptionMapsAsync(
        Exception exception,
        ExternalSourceProviderFailureKind expectedFailureKind,
        string expectedDiagnosticCode = ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed)
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-exception-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            throw exception;
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(expectedFailureKind, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == expectedDiagnosticCode);
        Assert.All(result.Diagnostics, AssertSafeTransportDiagnostic);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    private static void AssertSafeTransportDiagnostic(
        ExternalSourceConfigurationDiagnostic diagnostic)
    {
        Assert.Contains(
            diagnostic.Severity,
            new[] { "warning", "error" });
        Assert.Equal("$repository", diagnostic.Location);
        Assert.DoesNotContain("secret", diagnostic.Code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", diagnostic.Location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", diagnostic.Code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", diagnostic.Location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", diagnostic.Code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", diagnostic.Location, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../BaselineMini.slnx")]
    [InlineData("src/../BaselineMini.slnx")]
    [InlineData("/outside/BaselineMini.slnx")]
    [InlineData("C:/outside/BaselineMini.slnx")]
    public async Task AcquireAsync_UnsafeSolutionPath_IsRejectedBeforeTransport(string solutionPath)
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-path-");
        var transport = new ExternalSourceRecordingTransport((_, _, _) => ExternalSourceRepositoryTransportResult.Success("unused"));
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping(solutionPath));

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionPathInvalid);
        Assert.Equal(0, transport.CallCount);
        Assert.Empty(Directory.EnumerateDirectories(staging.DirectoryPath, "checkout-*"));
    }

    [Fact]
    public async Task AcquireAsync_TransportResultWithoutCheckout_IsRejectedAndCleaned()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-invalid-result-");
        var transport = new ExternalSourceRecordingTransport((_, _, _) => ExternalSourceRepositoryTransportResult.Success("unused"));
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task AcquireAsync_MissingSolution_IsRejectedAndCleansCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-solution-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            return ExternalSourceRepositoryTransportResult.Success("revision-42");
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task AcquireAsync_RejectsMappingWithCredentialsWithoutExposingUrl()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-credentials-");
        var transport = new ExternalSourceRecordingTransport((_, _, _) => ExternalSourceRepositoryTransportResult.Success("unused"));
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var mapping = new ExternalSourceMapping(
            "https://user:secret@gitea.example/shared.git",
            "BaselineMini.slnx",
            ["BaselineMini"]);

        var result = await acquirer.AcquireAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData("https://gitea.example/shared.git?branch=main")]
    [InlineData("https://gitea.example/shared.git#main")]
    [InlineData("https://build-user@gitea.example/shared.git")]
    [InlineData("ftp://gitea.example/shared.git")]
    public async Task AcquireAsync_RejectsNonCanonicalRepositoryUrlBeforeTransport(string url)
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-url-policy-");
        var transport = new ExternalSourceRecordingTransport((_, _, _) =>
            ExternalSourceRepositoryTransportResult.Success("unused"));
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var mapping = new ExternalSourceMapping(url, "BaselineMini.slnx", ["BaselineMini"]);

        var result = await acquirer.AcquireAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void Constructor_RejectsRelativeStagingRoot()
    {
        var transport = new ExternalSourceRecordingTransport((_, _, _) => ExternalSourceRepositoryTransportResult.Success("unused"));

        Assert.Throws<ArgumentException>(() =>
            new ExternalSourceRepositoryAcquirer(transport, "relative-staging-root"));
    }

    [Fact]
    public void TransportResult_RejectsAvailableResultWithoutRevision()
    {
        Assert.Throws<ArgumentException>(() =>
            new ExternalSourceRepositoryTransportResult(
                isAvailable: true,
                loadedRevision: " ",
                diagnostics: Array.Empty<ExternalSourceConfigurationDiagnostic>()));
    }

    [Fact]
    public void ReparsePointAttributes_AreRecognizedWithoutCreatingExternalLinks()
    {
        Assert.True(ExternalSourceRepositoryAcquirer.IsReparsePointAttribute(
            FileAttributes.Directory | FileAttributes.ReparsePoint));
        Assert.False(ExternalSourceRepositoryAcquirer.IsReparsePointAttribute(FileAttributes.Directory));
    }

    private static ExternalSourceMapping CreateMapping(string solutionPath = "BaselineMini.slnx") =>
        new(
            "https://gitea.example/shared.git",
            solutionPath,
            ["BaselineMini"]);

    private static ExternalSourceConfigurationDiagnostic Diagnostic(string code) =>
        new(code, "Testdiagnose", "warning", "test");

}
