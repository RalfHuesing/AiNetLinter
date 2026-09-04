#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class GiteaGitRepositoryCheckoutStatusTests
{
    [Fact]
    public async Task FetchDefaultBranchAsync_AllowsHarmlessGitWarningButSetsConcreteSafeDirectory()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-warning-");
        var destination = temp.CreateSubdirectory("checkout");
        Directory.CreateDirectory(Path.Combine(destination, ".git"));
        var executor = new RecordingGitExecutor((request, _) =>
            Task.FromResult(request.Arguments[0] is "status"
                ? new ExternalSourceGitProcessResult(0, string.Empty, "warning: repository maintenance is disabled\n")
                : request.Arguments[0] is "rev-parse"
                    ? CompletedProcess(ValidRevision)
                    : CompletedProcess()));
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.FetchDefaultBranchAsync(CreateMapping(), destination);

        Assert.True(result.IsAvailable);
        Assert.Equal("1", executor.Requests[0].Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("safe.directory", executor.Requests[0].Environment["GIT_CONFIG_KEY_0"]);
        Assert.Equal(Path.GetFullPath(destination), executor.Requests[0].Environment["GIT_CONFIG_VALUE_0"]);
    }

    [Fact]
    public async Task FetchDefaultBranchAsync_RejectsUnsafeRepositoryWarning()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-unsafe-warning-");
        var destination = temp.CreateSubdirectory("checkout");
        Directory.CreateDirectory(Path.Combine(destination, ".git"));
        var executor = new RecordingGitExecutor((request, _) =>
            Task.FromResult(request.Arguments[0] is "status"
                ? new ExternalSourceGitProcessResult(0, string.Empty, "warning: detected dubious ownership in repository\n")
                : CompletedProcess()));
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.FetchDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceCheckoutTrust.Unverified, result.CheckoutTrust);
        Assert.Single(executor.Requests);
    }

    [Theory]
    [InlineData(" M BaselineMini.slnx", 0, (int)ExternalSourceCheckoutTrust.Dirty, "external-source-repository-checkout-dirty")]
    [InlineData("!! ignored.txt", 0, (int)ExternalSourceCheckoutTrust.Dirty, "external-source-repository-checkout-dirty")]
    [InlineData("?? untracked.txt", 0, (int)ExternalSourceCheckoutTrust.Dirty, "external-source-repository-checkout-dirty")]
    [InlineData("??", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("malformed status", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("ZZ unknown-status", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("?! unknown-status", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("", 1, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("\n", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("\n?? .ainetlinter-owner", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData("?? .ainetlinter-owner\n\n", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    [InlineData(" M tracked.cs\nmalformed", 0, (int)ExternalSourceCheckoutTrust.Unverified, "external-source-repository-checkout-unverified")]
    public async Task FetchDefaultBranchAsync_RejectsDirtyOrUnverifiedCheckoutBeforeMutation(
        string statusOutput,
        int statusExitCode,
        int expectedTrustValue,
        string expectedDiagnosticCode)
    {
        using var temp = TestTempDirectory.Create("gitea-transport-status-gate-");
        var destination = temp.CreateSubdirectory("checkout");
        Directory.CreateDirectory(Path.Combine(destination, ".git"));
        var executor = new RecordingGitExecutor((request, _) =>
            Task.FromResult(request.Arguments[0] is "status"
                ? new ExternalSourceGitProcessResult(
                    statusExitCode,
                    statusOutput,
                    statusExitCode == 0 ? string.Empty : "sensitive-process-output")
                : CompletedProcess()));
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.FetchDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal((ExternalSourceCheckoutTrust)expectedTrustValue, result.CheckoutTrust);
        Assert.Equal(ExternalSourceRepositoryHealth.Unavailable, result.Health);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedDiagnosticCode);
        Assert.DoesNotContain(
            "sensitive-process-output",
            string.Join(" ", result.Diagnostics),
            StringComparison.Ordinal);
        Assert.Single(executor.Requests);
        Assert.Equal(
            ["status", "--porcelain=v1", "--untracked-files=all", "--ignored=all"],
            executor.Requests[0].Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?? .ainetlinter-owner")]
    [InlineData("?? .ainetlinter-owner\r\n?? .ainetlinter-owner\r\n")]
    public async Task FetchDefaultBranchAsync_AcceptsCompleteCleanStatusRecords(string statusOutput)
    {
        using var temp = TestTempDirectory.Create("gitea-transport-status-clean-");
        var destination = temp.CreateSubdirectory("checkout");
        Directory.CreateDirectory(Path.Combine(destination, ".git"));
        var executor = new RecordingGitExecutor((request, _) =>
            Task.FromResult(request.Arguments[0] is "status"
                ? CompletedProcess(statusOutput)
                : request.Arguments[0] is "rev-parse"
                    ? CompletedProcess(ValidRevision)
                    : CompletedProcess()));
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.FetchDefaultBranchAsync(CreateMapping(), destination);

        Assert.True(result.IsAvailable);
        Assert.Equal(ExternalSourceCheckoutTrust.Clean, result.CheckoutTrust);
        Assert.Equal(ExternalSourceRepositoryHealth.Verified, result.Health);
        Assert.Equal(ValidRevision, result.LoadedRevision);
        Assert.NotNull(result.CheckoutAttestation);
        Assert.Equal(5, executor.Requests.Count);
    }

    [Fact]
    public async Task FetchDefaultBranchAsync_RejectsCheckoutThatBecomesDirtyAfterRefresh()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-status-after-refresh-");
        var destination = temp.CreateSubdirectory("checkout");
        Directory.CreateDirectory(Path.Combine(destination, ".git"));
        var statusCallCount = 0;
        var executor = new RecordingGitExecutor((request, _) =>
        {
            if (request.Arguments[0] is "status" && statusCallCount++ == 1)
            {
                return Task.FromResult(CompletedProcess(" M BaselineMini.slnx"));
            }

            return Task.FromResult(CompletedProcess());
        });
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.FetchDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceCheckoutTrust.Dirty, result.CheckoutTrust);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutDirty);
        Assert.Equal(4, executor.Requests.Count);
    }

    private const string MappingUrl = "https://gitea.example/shared.git";
    private const string ValidRevision = "0123456789abcdef0123456789abcdef01234567";

    private static ExternalSourceMapping CreateMapping() =>
        new(MappingUrl, "BaselineMini.slnx", ["BaselineMini"]);

    private static ExternalSourceGitProcessResult CompletedProcess(string output = "") =>
        new(exitCode: 0, standardOutput: output, standardError: string.Empty);

    private sealed class RecordingGitExecutor : IExternalSourceGitProcessExecutor
    {
        private readonly Func<ExternalSourceGitProcessRequest, CancellationToken, Task<ExternalSourceGitProcessResult>> operation;

        internal RecordingGitExecutor(
            Func<ExternalSourceGitProcessRequest, CancellationToken, Task<ExternalSourceGitProcessResult>> operation)
        {
            this.operation = operation;
        }

        internal List<ExternalSourceGitProcessRequest> Requests { get; } = [];

        public Task<ExternalSourceGitProcessResult> ExecuteAsync(
            ExternalSourceGitProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return operation(request, cancellationToken);
        }
    }
}
