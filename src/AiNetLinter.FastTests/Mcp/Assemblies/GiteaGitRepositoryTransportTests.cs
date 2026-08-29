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
public sealed class GiteaGitRepositoryTransportTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string CredentialSecret = "transport-test-secret";
    private const string SecretEnvironmentVariable = "AINETLINTER_GIT_SECRET";
    private const string UsernameEnvironmentVariable = "AINETLINTER_GIT_USERNAME";

    [Fact]
    public async Task CloneDefaultBranchAsync_UsesSingleBranchNoTagsAndReturnsHeadRevision()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-success-");
        var destination = temp.CreateSubdirectory("checkout");
        var credential = new ExternalSourceCredential("build-user", CredentialSecret);
        var resolver = new RecordingCredentialResolver(credential);
        var executor = new RecordingGitExecutor((request, _) =>
        {
            if (request.Arguments[0] is "clone")
            {
                CreateCloneTree(request.WorkingDirectory);
                return Task.FromResult(CompletedProcess());
            }

            return Task.FromResult(CompletedProcess(Revision + Environment.NewLine));
        });
        var transport = new GiteaGitRepositoryTransport(resolver, executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.True(result.IsAvailable);
        Assert.Equal(Revision, result.LoadedRevision);
        Assert.Equal(2, executor.Requests.Count);
        var cloneRequest = executor.Requests[0];
        Assert.Equal("git", cloneRequest.FileName);
        Assert.Equal(
            ["clone", "--single-branch", "--no-tags", "--", MappingUrl, GiteaGitRepositoryTransport.CloneDirectoryName],
            cloneRequest.Arguments);
        Assert.DoesNotContain("--branch", cloneRequest.Arguments);
        Assert.Equal(destination, cloneRequest.WorkingDirectory);
        Assert.Equal("1", cloneRequest.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("credential.helper", cloneRequest.Environment["GIT_CONFIG_KEY_0"]);
        Assert.Equal(CredentialSecret, cloneRequest.Environment[SecretEnvironmentVariable]);
        Assert.DoesNotContain(CredentialSecret, string.Join(" ", cloneRequest.Arguments));
        Assert.False(resolver.CancellationToken.CanBeCanceled);
        Assert.Throws<ObjectDisposedException>(() => _ = credential.Secret);

        var headRequest = executor.Requests[1];
        Assert.Equal(["rev-parse", "--verify", "HEAD"], headRequest.Arguments);
        Assert.DoesNotContain(SecretEnvironmentVariable, headRequest.Environment.Keys);
        Assert.DoesNotContain(UsernameEnvironmentVariable, headRequest.Environment.Keys);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_WithoutResolverLeavesPublicClonePromptFree()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-public-");
        var destination = temp.CreateSubdirectory("checkout");
        var executor = new RecordingGitExecutor((request, _) =>
        {
            if (request.Arguments[0] is "clone")
            {
                CreateCloneTree(request.WorkingDirectory);
            }

            return Task.FromResult(CompletedProcess(
                request.Arguments[0] is "clone" ? string.Empty : Revision));
        });
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.True(result.IsAvailable);
        Assert.Equal(Revision, result.LoadedRevision);
        var environment = executor.Requests[0].Environment;
        Assert.Equal("0", environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("0", environment["GIT_TERMINAL_PROMPT"]);
        Assert.Equal(string.Empty, environment["GIT_ASKPASS"]);
        Assert.DoesNotContain(SecretEnvironmentVariable, environment.Keys);
        Assert.DoesNotContain(UsernameEnvironmentVariable, environment.Keys);
    }

    [Theory]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 400", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 401", false, (int)ExternalSourceProviderFailureKind.AuthenticationRequired)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 401", true, (int)ExternalSourceProviderFailureKind.AccessDenied)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 403", false, (int)ExternalSourceProviderFailureKind.AccessDenied)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 404", false, (int)ExternalSourceProviderFailureKind.RepositoryNotFound)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 500", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': The requested URL returned error: 401\nrepository not found", false, (int)ExternalSourceProviderFailureKind.AuthenticationRequired)]
    [InlineData("fatal: repository not found", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: 403 forbidden", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: unable to access: Could not resolve host", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: unable to access 'https://gitea.example/shared.git': Could not resolve host: gitea.example", false, (int)ExternalSourceProviderFailureKind.NetworkUnavailable)]
    [InlineData("fatal: protocol error", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("fatal: Die angeforderte Antwort ist ungültig.", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    [InlineData("unbekannte Transportausgabe 404", false, (int)ExternalSourceProviderFailureKind.InvalidResponse)]
    public async Task CloneDefaultBranchAsync_MapsExitOutputToTypedFailure(
        string errorOutput,
        bool hasCredential,
        int expectedFailureKindValue)
    {
        using var temp = TestTempDirectory.Create("gitea-transport-failure-");
        var destination = temp.CreateSubdirectory("checkout");
        var executor = new RecordingGitExecutor((_, _) =>
            Task.FromResult(new ExternalSourceGitProcessResult(128, string.Empty, errorOutput)));
        var resolver = hasCredential
            ? new RecordingCredentialResolver(new ExternalSourceCredential("build-user", CredentialSecret))
            : null;
        var transport = new GiteaGitRepositoryTransport(resolver, executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal(
            (ExternalSourceProviderFailureKind)expectedFailureKindValue,
            result.FailureKind);
        Assert.Null(result.LoadedRevision);
        Assert.NotEmpty(result.Diagnostics);
        Assert.DoesNotContain(errorOutput, result.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Single(executor.Requests);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_TimeoutUsesTypedTimeoutFailure()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-timeout-");
        var destination = temp.CreateSubdirectory("checkout");
        var executor = new RecordingGitExecutor((_, _) =>
            Task.FromResult(new ExternalSourceGitProcessResult(
                exitCode: -1,
                standardOutput: string.Empty,
                standardError: "secret timeout output",
                new ExternalSourceGitProcessResultOptions { WasTimedOut = true })));
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.Timeout, result.FailureKind);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.Timeout);
        Assert.DoesNotContain("secret", result.Diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_NeverProjectsCredentialSecretIntoFailureDiagnostic()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-secret-");
        var destination = temp.CreateSubdirectory("checkout");
        var resolver = new RecordingCredentialResolver(
            new ExternalSourceCredential("build-user", CredentialSecret));
        var executor = new RecordingGitExecutor((_, _) =>
            Task.FromResult(new ExternalSourceGitProcessResult(
                exitCode: 128,
                standardOutput: string.Empty,
                standardError: $"fatal: authentication failed: {CredentialSecret}")));
        var transport = new GiteaGitRepositoryTransport(resolver, executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.Equal(ExternalSourceProviderFailureKind.AccessDenied, result.FailureKind);
        Assert.DoesNotContain(
            CredentialSecret,
            result.Diagnostics[0].Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_RejectsInvalidHeadRevision()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-revision-");
        var destination = temp.CreateSubdirectory("checkout");
        var executor = new RecordingGitExecutor((request, _) =>
        {
            if (request.Arguments[0] is "clone")
            {
                CreateCloneTree(request.WorkingDirectory);
                return Task.FromResult(CompletedProcess());
            }

            return Task.FromResult(CompletedProcess("not-a-git-revision"));
        });
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);

        var result = await transport.CloneDefaultBranchAsync(CreateMapping(), destination);

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Null(result.LoadedRevision);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_CancellationAbortsExecutorAndKeepsCancellation()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-cancel-");
        var destination = temp.CreateSubdirectory("checkout");
        using var cancellation = new CancellationTokenSource();
        var executorProcessWasAborted = false;
        var resolver = new RecordingCredentialResolver(null);
        var executor = new RecordingGitExecutor(async (_, token) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CompletedProcess();
            }
            catch (OperationCanceledException)
            {
                executorProcessWasAborted = true;
                throw new OperationCanceledException(token);
            }
        });
        var transport = new GiteaGitRepositoryTransport(resolver, executor);
        var operation = transport.CloneDefaultBranchAsync(CreateMapping(), destination, cancellation.Token).AsTask();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(executorProcessWasAborted);
        Assert.Equal(cancellation.Token, resolver.CancellationToken);
        Assert.Equal(cancellation.Token, executor.LastCancellationToken);
    }

    [Fact]
    public async Task CloneDefaultBranchAsync_ThroughAcquirerCleansPartialCheckoutAfterFailure()
    {
        using var temp = TestTempDirectory.Create("gitea-transport-cleanup-");
        var executor = new RecordingGitExecutor((request, _) =>
        {
            CreateCloneTree(request.WorkingDirectory, includeSolution: false);
            return Task.FromResult(new ExternalSourceGitProcessResult(
                exitCode: 128,
                standardOutput: string.Empty,
                standardError: "fatal: repository not found"));
        });
        var transport = new GiteaGitRepositoryTransport(processExecutor: executor);
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, temp);

        var result = await acquirer.AcquireAsync(CreateMapping());

        Assert.False(result.IsAvailable);
        Assert.Equal(ExternalSourceProviderFailureKind.InvalidResponse, result.FailureKind);
        Assert.Null(result.Checkout);
        Assert.Empty(Directory.EnumerateDirectories(temp.DirectoryPath, "checkout-*"));
    }

    private const string MappingUrl = "https://gitea.example/shared.git";

    private static ExternalSourceMapping CreateMapping() =>
        new(MappingUrl, "BaselineMini.slnx", ["BaselineMini"]);

    private static ExternalSourceGitProcessResult CompletedProcess(string output = "") =>
        new(exitCode: 0, standardOutput: output, standardError: string.Empty);

    private static void CreateCloneTree(string destinationPath, bool includeSolution = true)
    {
        var clonePath = Path.Combine(destinationPath, GiteaGitRepositoryTransport.CloneDirectoryName);
        Directory.CreateDirectory(Path.Combine(clonePath, ".git"));
        if (includeSolution)
        {
            File.WriteAllText(Path.Combine(clonePath, "BaselineMini.slnx"), string.Empty);
        }
    }

    private sealed class RecordingCredentialResolver : IExternalSourceCredentialResolver
    {
        private readonly ExternalSourceCredential? credential;

        internal RecordingCredentialResolver(ExternalSourceCredential? credential)
        {
            this.credential = credential;
        }

        internal CancellationToken CancellationToken { get; private set; }

        public ValueTask<ExternalSourceCredential?> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(credential);
        }
    }

    private sealed class RecordingGitExecutor : IExternalSourceGitProcessExecutor
    {
        private readonly Func<ExternalSourceGitProcessRequest, CancellationToken, Task<ExternalSourceGitProcessResult>> operation;

        internal RecordingGitExecutor(
            Func<ExternalSourceGitProcessRequest, CancellationToken, Task<ExternalSourceGitProcessResult>> operation)
        {
            this.operation = operation;
        }

        internal List<ExternalSourceGitProcessRequest> Requests { get; } = [];

        internal CancellationToken LastCancellationToken { get; private set; }

        public Task<ExternalSourceGitProcessResult> ExecuteAsync(
            ExternalSourceGitProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            LastCancellationToken = cancellationToken;
            return operation(request, cancellationToken);
        }
    }
}
