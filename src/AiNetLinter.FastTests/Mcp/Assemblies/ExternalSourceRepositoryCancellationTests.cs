#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Serilog;
using Serilog.Events;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryCancellationTests
{
    [Fact]
    public async Task AcquireAsync_CancellationCleanupFailure_IsLoggedWithoutChangingCancellation()
    {
        using var staging = TestTempDirectory.Create("external-source-cancellation-cleanup-");
        using var cancellation = new CancellationTokenSource();
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "BaselineMini.slnx",
            ["BaselineMini"]);
        var sink = new ExternalSourceRepositoryTestLogSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var transport = new CancellationCleanupFailureTransport(cancellation);
        var acquirer = new ExternalSourceRepositoryAcquirer(
            transport,
            staging.DirectoryPath,
            logger,
            new LocalExternalSourceRepositoryCacheWriter(staging.DirectoryPath));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            acquirer.AcquireAsync(mapping, cancellation.Token));

        Assert.Same(transport.ThrownException, exception);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(cancellation.Token, transport.CancellationToken);
        Assert.NotNull(transport.DestinationPath);
        var destinationPath = transport.DestinationPath!;
        Assert.True(Directory.Exists(destinationPath));
        Assert.False(File.Exists(Path.Combine(
            destinationPath,
            ExternalSourceCheckoutOwnership.OwnershipMarkerFileName)));
        var quarantineRoot = Path.Combine(staging.DirectoryPath, ".quarantine");
        var quarantineMetadata = Assert.Single(Directory.EnumerateFiles(
            quarantineRoot,
            "checkout-*.json"));
        var quarantineText = File.ReadAllText(quarantineMetadata);
        Assert.Contains("ainetlinter-source-acquirer", quarantineText, StringComparison.Ordinal);
        Assert.Contains("Checkout konnte", quarantineText, StringComparison.Ordinal);
        Assert.Contains("ExpiresUtc", quarantineText, StringComparison.Ordinal);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
        Assert.True(logEvent.Properties.TryGetValue("Code", out var codeProperty));
        var code = Assert.IsType<ScalarValue>(codeProperty).Value;
        Assert.Equal(
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
            code);
        var renderedMessage = logEvent.RenderMessage();
        Assert.Contains(
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
            renderedMessage,
            StringComparison.Ordinal);
        Assert.Null(logEvent.Exception);
        Assert.DoesNotContain(destinationPath, renderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(transport.OwnershipToken!, renderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(mapping.Url, renderedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(transport.ExceptionText, renderedMessage, StringComparison.Ordinal);
    }

    private sealed class CancellationCleanupFailureTransport : IGiteaRepositoryTransport
    {
        private readonly CancellationTokenSource cancellation;

        internal CancellationCleanupFailureTransport(CancellationTokenSource cancellation)
        {
            this.cancellation = cancellation;
        }

        internal string? DestinationPath { get; private set; }

        internal string? OwnershipToken { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        internal string ExceptionText { get; } = "cleanup cancellation secret";

        internal OperationCanceledException? ThrownException { get; private set; }

        public ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            DestinationPath = destinationPath;
            CancellationToken = cancellationToken;
            var markerPath = Path.Combine(
                destinationPath,
                ExternalSourceCheckoutOwnership.OwnershipMarkerFileName);
            OwnershipToken = File.ReadAllText(markerPath);
            File.Delete(markerPath);
            cancellation.Cancel();
            ThrownException = new OperationCanceledException(ExceptionText, cancellationToken);
            throw ThrownException;
        }

        public ValueTask<ExternalSourceRepositoryTransportResult> FetchDefaultBranchAsync(
            ExternalSourceMapping mapping,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            CloneDefaultBranchAsync(mapping, destinationPath, cancellationToken);
    }
}
