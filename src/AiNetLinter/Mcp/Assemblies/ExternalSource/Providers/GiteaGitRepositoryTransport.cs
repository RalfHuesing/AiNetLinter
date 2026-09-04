#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Providers;

internal sealed class GiteaGitRepositoryTransport : IGiteaRepositoryTransport
{
    internal const string CloneDirectoryName = ".ainetlinter-git-clone";

    private const string GitCommand = "git";
    private const string GitTerminalPromptVariable = "GIT_TERMINAL_PROMPT";
    private const string GitAskPassVariable = "GIT_ASKPASS";
    private const string GitConfigNoSystemVariable = "GIT_CONFIG_NOSYSTEM";
    private const string GitConfigGlobalVariable = "GIT_CONFIG_GLOBAL";
    private const string GitConfigSystemVariable = "GIT_CONFIG_SYSTEM";
    private const string GitConfigCountVariable = "GIT_CONFIG_COUNT";
    private const string GitConfigKeyPrefix = "GIT_CONFIG_KEY_0";
    private const string GitConfigValuePrefix = "GIT_CONFIG_VALUE_0";
    private const string GitNoTagsArgument = "--no-tags";
    // ainetlinter-disable MagicValues — feste Child-Process-Namen, keine Secret-Werte.
    private const string CredentialUsernameVariable = "AINETLINTER_GIT_USERNAME";
    // ainetlinter-disable MagicValues — feste Child-Process-Namen, keine Secret-Werte.
    private const string CredentialSecretVariable = "AINETLINTER_GIT_SECRET";
    // ainetlinter-disable MagicValues — Git-Credential-Helper-Code, der nur Child-Process-Umgebung liest.
    private const string CredentialHelperValue =
        "!f() { case \"$1\" in get) printf 'username=%s\\npassword=%s\\n' \"$AINETLINTER_GIT_USERNAME\" \"$AINETLINTER_GIT_SECRET\";; esac; }; f";
    private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes(5);

    private readonly IExternalSourceCredentialResolver? credentialResolver;
    private readonly IExternalSourceGitProcessExecutor processExecutor;
    private readonly TimeSpan processTimeout;

    internal GiteaGitRepositoryTransport(
        IExternalSourceCredentialResolver? credentialResolver = null,
        IExternalSourceGitProcessExecutor? processExecutor = null,
        TimeSpan? processTimeout = null)
    {
        this.credentialResolver = credentialResolver;
        this.processExecutor = processExecutor ?? new ExternalSourceGitProcessExecutor();
        this.processTimeout = processTimeout ?? DefaultProcessTimeout;
        if (this.processTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(processTimeout));
        }
    }

    public async ValueTask<ExternalSourceRepositoryTransportResult> CloneDefaultBranchAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!ExternalSourceUrlPolicy.TryNormalize(mapping.Url, out var repositoryUrl))
        {
            return Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidDestination(destinationPath))
        {
            return Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid);
        }

        return await ExecuteCloneWorkflowAsync(
                mapping,
                repositoryUrl!,
                destinationPath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<ExternalSourceRepositoryTransportResult> FetchDefaultBranchAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!ExternalSourceUrlPolicy.TryNormalize(mapping.Url, out _))
        {
            return Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidFetchDestination(destinationPath))
        {
            return Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid);
        }

        return await ExecuteFetchWorkflowAsync(
                mapping,
                destinationPath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ExternalSourceRepositoryTransportResult> ExecuteCloneWorkflowAsync(
        ExternalSourceMapping mapping,
        string repositoryUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ExternalSourceCredential? credential = null;
        try
        {
            credential = await ResolveCredentialAsync(mapping, cancellationToken)
                .ConfigureAwait(false);
            var cloneResult = await ExecuteCloneAsync(
                    repositoryUrl,
                    destinationPath,
                    credential,
                    cancellationToken)
                .ConfigureAwait(false);
            if (cloneResult is not null)
            {
                return cloneResult;
            }

            credential?.Dispose();
            credential = null;
            return await ExecuteVerifiedHeadAsync(destinationPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureKind = ExternalSourceRepositoryFailurePolicy.ClassifyTransportException(exception);
            return Failure(
                failureKind,
                ExternalSourceRepositoryFailurePolicy.GetTransportDiagnosticCode(exception));
        }
        finally
        {
            credential?.Dispose();
        }
    }

    private async Task<ExternalSourceRepositoryTransportResult> ExecuteFetchWorkflowAsync(
        ExternalSourceMapping mapping,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        ExternalSourceCredential? credential = null;
        try
        {
            var statusFailure = await ExecuteStatusAsync(destinationPath, cancellationToken)
                .ConfigureAwait(false);
            if (statusFailure is not null) return statusFailure;
            credential = await ResolveCredentialAsync(mapping, cancellationToken)
                .ConfigureAwait(false);
            var fetchResult = await ExecuteFetchAsync(
                    destinationPath,
                    credential,
                    cancellationToken)
                .ConfigureAwait(false);
            if (fetchResult is not null)
            {
                return fetchResult;
            }

            credential?.Dispose();
            credential = null;
            var resetResult = await ExecuteResetAsync(destinationPath, cancellationToken).ConfigureAwait(false);
            if (resetResult is not null) return resetResult;
            var refreshedStatusFailure = await ExecuteStatusAsync(destinationPath, cancellationToken)
                .ConfigureAwait(false);
            if (refreshedStatusFailure is not null)
            {
                return refreshedStatusFailure;
            }

            return await ExecuteHeadAsync(destinationPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureKind = ExternalSourceRepositoryFailurePolicy.ClassifyTransportException(exception);
            return Failure(
                failureKind,
                ExternalSourceRepositoryFailurePolicy.GetTransportDiagnosticCode(exception));
        }
        finally
        {
            credential?.Dispose();
        }
    }

    private async Task<ExternalSourceRepositoryTransportResult?> ExecuteStatusAsync(
        string destinationPath,
        CancellationToken cancellationToken) =>
        await ExternalSourceRepositoryCheckoutStatus.ExecuteAsync(
                processExecutor,
                destinationPath,
                processTimeout,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<ExternalSourceRepositoryTransportResult> ExecuteVerifiedHeadAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var statusFailure = await ExecuteStatusAsync(destinationPath, cancellationToken)
            .ConfigureAwait(false);
        return statusFailure
            ?? await ExecuteHeadAsync(destinationPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExternalSourceCredential?> ResolveCredentialAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken)
    {
        if (credentialResolver is null)
        {
            return null;
        }

        return await credentialResolver.ResolveAsync(mapping, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ExternalSourceRepositoryTransportResult?> ExecuteCloneAsync(
        string repositoryUrl,
        string destinationPath,
        ExternalSourceCredential? credential,
        CancellationToken cancellationToken)
    {
        var request = new ExternalSourceGitProcessRequest(
            GitCommand,
            [
                "clone",
                "--single-branch",
                GitNoTagsArgument,
                "--",
                repositoryUrl,
                CloneDirectoryName,
            ],
            destinationPath,
            processTimeout,
            CreateEnvironment(credential, destinationPath));
        var processResult = await processExecutor.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var failure = CreateProcessFailure(processResult, credential is not null, operation: "clone");
        if (failure is not null)
        {
            return failure;
        }

        return TryPromoteClone(destinationPath)
            ? null
            : Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid);
    }

    private async Task<ExternalSourceRepositoryTransportResult?> ExecuteFetchAsync(
        string destinationPath,
        ExternalSourceCredential? credential,
        CancellationToken cancellationToken)
    {
        var request = new ExternalSourceGitProcessRequest(
            GitCommand,
            ["fetch", GitNoTagsArgument, "origin"],
            destinationPath,
            processTimeout,
            CreateEnvironment(credential, destinationPath));
        var processResult = await processExecutor.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return CreateProcessFailure(processResult, credential is not null);
    }

    private async Task<ExternalSourceRepositoryTransportResult?> ExecuteResetAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var request = new ExternalSourceGitProcessRequest(
            GitCommand,
            ["reset", "--hard", "origin/HEAD"],
            destinationPath,
            processTimeout,
            CreateEnvironment(credential: null, destinationPath));
        var processResult = await processExecutor.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return CreateProcessFailure(processResult, hasCredential: false);
    }

    private async Task<ExternalSourceRepositoryTransportResult> ExecuteHeadAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var request = new ExternalSourceGitProcessRequest(
            GitCommand,
            ["rev-parse", "--verify", "HEAD"],
            destinationPath,
            processTimeout,
            CreateEnvironment(credential: null, destinationPath));
        var processResult = await processExecutor.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var failure = CreateProcessFailure(processResult, hasCredential: false);
        if (failure is not null)
        {
            return failure;
        }

        return TryParseRevision(processResult.StandardOutput, out var revision)
            ? ExternalSourceRepositoryTransportResult.Success(
                revision!,
                ExternalSourceCheckoutAttestation.FromTransport(
                    destinationPath,
                    revision!,
                    ExecuteVerifiedHeadAsync))
            : Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid);
    }

    private static ExternalSourceRepositoryTransportResult? CreateProcessFailure(
        ExternalSourceGitProcessResult? processResult,
        bool hasCredential,
        string? operation = null)
    {
        if (processResult is null)
        {
            return Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid);
        }

        if (processResult.ExitCode == 0
            && !processResult.WasTimedOut
            && !processResult.StandardErrorTruncated
            && ExternalSourceGitProcessOutputPolicy.IsHarmlessStandardError(
                processResult.StandardError,
                operation))
        {
            return null;
        }

        var failureKind = ExternalSourceRepositoryFailurePolicy.ClassifyGitProcessFailure(
            processResult,
            hasCredential);
        return Failure(
            failureKind,
            ExternalSourceRepositoryFailurePolicy.GetFailureDiagnosticCode(failureKind));
    }

    private static bool TryPromoteClone(string destinationPath)
    {
        var clonePath = Path.Combine(destinationPath, CloneDirectoryName);
        if (!Directory.Exists(clonePath)
            || ExternalSourceRepositoryPathGuard.ContainsActualReparsePointOnPath(clonePath)
            || ExternalSourceRepositoryPathGuard.ContainsActualReparsePointInTree(clonePath))
        {
            return false;
        }

        var entries = new List<string>(Directory.EnumerateFileSystemEntries(clonePath));
        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name)
                || string.Equals(
                    name,
                    ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                    StringComparison.OrdinalIgnoreCase)
                || File.Exists(Path.Combine(destinationPath, name))
                || Directory.Exists(Path.Combine(destinationPath, name)))
            {
                return false;
            }
        }

        foreach (var entry in entries)
        {
            MoveEntry(entry, Path.Combine(destinationPath, Path.GetFileName(entry)));
        }

        Directory.Delete(clonePath);
        return !Directory.Exists(clonePath);
    }

    private static void MoveEntry(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    private static Dictionary<string, string> CreateEnvironment(
        ExternalSourceCredential? credential,
        string destinationPath)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GitTerminalPromptVariable] = "0",
            [GitAskPassVariable] = string.Empty,
            [GitConfigNoSystemVariable] = "1",
            [GitConfigGlobalVariable] = GetGitNullConfigPath(),
            [GitConfigSystemVariable] = GetGitNullConfigPath(),
            [GitConfigCountVariable] = credential is null ? "1" : "2",
            [GitConfigKeyPrefix] = "safe.directory",
            [GitConfigValuePrefix] = Path.GetFullPath(destinationPath),
        };
        if (credential is not null)
        {
            environment["GIT_CONFIG_KEY_1"] = "credential.helper";
            environment["GIT_CONFIG_VALUE_1"] = CredentialHelperValue;
            environment[CredentialUsernameVariable] = credential.Username;
            environment[CredentialSecretVariable] = credential.Secret;
        }

        return environment;
    }

    private static string GetGitNullConfigPath() =>
        OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    private static bool IsValidDestination(string destinationPath) =>
        !string.IsNullOrWhiteSpace(destinationPath)
        && Path.IsPathFullyQualified(destinationPath)
        && Directory.Exists(destinationPath)
        && !ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(destinationPath)
        && !ExternalSourceRepositoryPathGuard.ContainsActualReparsePointInTree(destinationPath);

    private static bool IsValidFetchDestination(string destinationPath) =>
        IsValidDestination(destinationPath)
        && Directory.Exists(Path.Combine(destinationPath, ".git"))
        && !ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(
            Path.Combine(destinationPath, ".git"));

    private static bool TryParseRevision(string output, out string? revision)
    {
        revision = output.Trim();
        if (revision.Length is not (40 or 64))
        {
            revision = null;
            return false;
        }

        foreach (var character in revision)
        {
            if (!IsHexDigit(character))
            {
                revision = null;
                return false;
            }
        }

        return true;
    }

    private static bool IsHexDigit(char character) =>
        character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F';

    private static ExternalSourceRepositoryTransportResult Failure(
        ExternalSourceProviderFailureKind failureKind,
        string diagnosticCode,
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified) =>
        new(
            isAvailable: false,
            loadedRevision: null,
            diagnostics: [ExternalSourceConfigurationDiagnostic.CreateError(
                diagnosticCode,
                "Die Repository-Akquisition ist fehlgeschlagen.",
                nameof(GiteaGitRepositoryTransport),
                "$repository")],
            checkoutTrust: checkoutTrust,
            state: ExternalSourceRepositoryResultState.Create(failureKind));

}
