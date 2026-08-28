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
        var transport = new RecordingTransport((_, destination, _) =>
        {
            CopySolution(fixture.RootPath, destination);
            return Success("revision-42");
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

        var result = await acquirer.AcquireAsync(mapping);

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.None, result.FailureKind);
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        Assert.Equal("revision-42", result.LoadedRevision);
        Assert.Equal("revision-42", checkout.LoadedRevision);
        Assert.True(File.Exists(checkout.SolutionPath));
        Assert.Equal(Path.Combine(checkout.CheckoutPath, "BaselineMini.slnx"), checkout.SolutionPath);
        Assert.StartsWith(staging.DirectoryPath + Path.DirectorySeparatorChar, checkout.CheckoutPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(transport.DestinationExistedAtCall);
        Assert.Same(mapping, transport.Mapping);

        checkout.Dispose();
        checkout.Dispose();

        Assert.True(File.Exists(foreignFile));
        Assert.False(Directory.Exists(checkout.CheckoutPath));
        Assert.True(checkout.IsDisposed);
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
        var transport = new RecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [Diagnostic("transport-warning")],
                failureKind: (ExternalSourceProviderFailureKind)failureKindValue);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal((ExternalSourceProviderFailureKind)failureKindValue, result.FailureKind);
        Assert.Null(result.Checkout);
        Assert.Null(result.LoadedRevision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "transport-warning");
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task AcquireAsync_Cancellation_RethrowsAndCleansOwnCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-cancel-");
        using var cancellation = new CancellationTokenSource();
        var transport = new RecordingTransport((_, destination, token) =>
        {
            Directory.CreateDirectory(destination);
            cancellation.Cancel();
            throw new OperationCanceledException(token);
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

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
        var transport = new RecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "partial.txt"), "partial");
            throw new InvalidOperationException("secret transport detail");
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Theory]
    [InlineData("../BaselineMini.slnx")]
    [InlineData("src/../BaselineMini.slnx")]
    [InlineData("/outside/BaselineMini.slnx")]
    [InlineData("C:/outside/BaselineMini.slnx")]
    public async Task AcquireAsync_UnsafeSolutionPath_IsRejectedBeforeTransport(string solutionPath)
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-path-");
        var transport = new RecordingTransport((_, _, _) => Success("unused"));
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

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
        var transport = new RecordingTransport((_, _, _) => Success("unused"));
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid);
        Assert.False(Directory.Exists(transport.DestinationPath));
    }

    [Fact]
    public async Task AcquireAsync_MissingSolution_IsRejectedAndCleansCheckout()
    {
        using var staging = TestTempDirectory.Create("external-source-acquirer-solution-");
        var transport = new RecordingTransport((_, destination, _) =>
        {
            Directory.CreateDirectory(destination);
            return Success("revision-42");
        });
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);

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
        var transport = new RecordingTransport((_, _, _) => Success("unused"));
        var acquirer = new ExternalSourceRepositoryAcquirer(transport, staging.DirectoryPath);
        var mapping = new ExternalSourceMapping(
            "https://user:secret@gitea.example/shared.git",
            "BaselineMini.slnx",
            ["BaselineMini"]);

        var result = await acquirer.AcquireAsync(mapping);

        Assert.False(result.IsAvailable);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("secret", StringComparison.Ordinal));
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void Constructor_RejectsRelativeStagingRoot()
    {
        var transport = new RecordingTransport((_, _, _) => Success("unused"));

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

    private static ExternalSourceRepositoryTransportResult Success(string revision) =>
        new(
            isAvailable: true,
            loadedRevision: revision,
            diagnostics: Array.Empty<ExternalSourceConfigurationDiagnostic>());

    private static ExternalSourceConfigurationDiagnostic Diagnostic(string code) =>
        new(code, "Testdiagnose", "warning", "test");

    private static void CopySolution(string sourceRoot, string destination)
    {
        Directory.CreateDirectory(destination);
        File.Copy(
            Path.Combine(sourceRoot, "BaselineMini.slnx"),
            Path.Combine(destination, "BaselineMini.slnx"));
    }

    private sealed class RecordingTransport : IGiteaRepositoryTransport
    {
        private readonly Func<ExternalSourceMapping, string, CancellationToken, ExternalSourceRepositoryTransportResult> operation;

        internal RecordingTransport(
            Func<ExternalSourceMapping, string, CancellationToken, ExternalSourceRepositoryTransportResult> operation)
        {
            this.operation = operation;
        }

        internal int CallCount { get; private set; }

        internal ExternalSourceMapping? Mapping { get; private set; }

        internal string? DestinationPath { get; private set; }

        internal bool DestinationExistedAtCall { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        public ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Mapping = mapping;
            DestinationPath = destinationPath;
            DestinationExistedAtCall = Directory.Exists(destinationPath) || File.Exists(destinationPath);
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(operation(mapping, destinationPath, cancellationToken));
        }
    }
}
