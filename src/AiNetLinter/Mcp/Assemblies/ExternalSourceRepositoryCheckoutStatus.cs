#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCheckoutStatus
{
    // ainetlinter-disable MagicValues — feste Git-Status-Schnittstelle.
    private const string GitCommand = "git";
    // ainetlinter-disable MagicValues — feste Git-Status-Schnittstelle.
    private const string StatusCommand = "status";
    // ainetlinter-disable MagicValues — feste Git-Status-Schnittstelle.
    private const string StatusPorcelainOption = "--porcelain=v1";
    // ainetlinter-disable MagicValues — feste Git-Status-Schnittstelle.
    private const string StatusUntrackedOption = "--untracked-files=all";
    // ainetlinter-disable MagicValues — feste Git-Status-Schnittstelle.
    private const string StatusIgnoredOption = "--ignored=all";
    // ainetlinter-disable MagicValues — festes Git-Status-Präfix.
    private const string UntrackedStatusPrefix = "?? ";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitTerminalPromptVariable = "GIT_TERMINAL_PROMPT";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitAskPassVariable = "GIT_ASKPASS";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitConfigNoSystemVariable = "GIT_CONFIG_NOSYSTEM";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitConfigGlobalVariable = "GIT_CONFIG_GLOBAL";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitConfigSystemVariable = "GIT_CONFIG_SYSTEM";
    // ainetlinter-disable MagicValues — feste nicht-interaktive Git-Umgebung.
    private const string GitConfigCountVariable = "GIT_CONFIG_COUNT";

    internal static async Task<ExternalSourceRepositoryTransportResult?> ExecuteAsync(
        IExternalSourceGitProcessExecutor processExecutor,
        string destinationPath,
        TimeSpan processTimeout,
        CancellationToken cancellationToken)
    {
        var request = new ExternalSourceGitProcessRequest(
            GitCommand,
            [StatusCommand, StatusPorcelainOption, StatusUntrackedOption, StatusIgnoredOption],
            destinationPath,
            processTimeout,
            CreateEnvironment());
        var processResult = await processExecutor.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (processResult is null
            || processResult.ExitCode != 0
            || processResult.WasTimedOut
            || processResult.StandardOutputTruncated
            || processResult.StandardErrorTruncated
            || !string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            return Failure(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified,
                ExternalSourceCheckoutTrust.Unverified);
        }

        var checkoutTrust = AssessStatus(processResult.StandardOutput);
        return checkoutTrust is null
            ? null
            : Failure(
                checkoutTrust is ExternalSourceCheckoutTrust.Dirty
                    ? ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutDirty
                    : ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified,
                checkoutTrust.Value);
    }

    private static ExternalSourceCheckoutTrust? AssessStatus(string statusOutput)
    {
        foreach (var line in statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalizedLine = line.TrimEnd('\r');
            if (normalizedLine.Length < 3
                || !IsStatusCode(normalizedLine[0])
                || !IsStatusCode(normalizedLine[1])
                || !IsValidStatusPair(normalizedLine[0], normalizedLine[1])
                || normalizedLine[2] is not ' ')
            {
                return ExternalSourceCheckoutTrust.Unverified;
            }

            if (normalizedLine.StartsWith(UntrackedStatusPrefix, StringComparison.Ordinal)
                && string.Equals(
                    normalizedLine[3..],
                    ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            return ExternalSourceCheckoutTrust.Dirty;
        }

        return null;
    }

    private static bool IsStatusCode(char value) =>
        value is ' ' or 'M' or 'A' or 'D' or 'R' or 'C' or 'U' or 'T' or '?' or '!';

    private static bool IsValidStatusPair(char first, char second) =>
        first is '?' or '!'
            ? first == second
            : second is not '?' and not '!';

    private static ExternalSourceRepositoryTransportResult Failure(
        string diagnosticCode,
        ExternalSourceCheckoutTrust checkoutTrust) =>
        new(
            isAvailable: false,
            loadedRevision: null,
            diagnostics: [ExternalSourceConfigurationDiagnostic.CreateError(
                diagnosticCode,
                "Der Repository-Checkout konnte nicht als sauber verifiziert werden.",
                nameof(ExternalSourceRepositoryCheckoutStatus),
                "$repository")],
            checkoutTrust: checkoutTrust,
            state: ExternalSourceRepositoryResultState.Create(
                ExternalSourceProviderFailureKind.InvalidResponse));

    private static System.Collections.Generic.Dictionary<string, string> CreateEnvironment() =>
        new(StringComparer.Ordinal)
        {
            [GitTerminalPromptVariable] = "0",
            [GitAskPassVariable] = string.Empty,
            [GitConfigNoSystemVariable] = "1",
            [GitConfigGlobalVariable] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            [GitConfigSystemVariable] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            [GitConfigCountVariable] = "0",
        };
}
