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
        if (statusOutput.Length == 0)
        {
            return null;
        }

        var records = statusOutput.Split('\n');
        var recordCount = records.Length - (records[^1].Length == 0 ? 1 : 0);
        if (recordCount == 0)
        {
            return ExternalSourceCheckoutTrust.Unverified;
        }

        var hasDirtyRecord = false;
        for (var index = 0; index < recordCount; index++)
        {
            if (!TryNormalizeStatusRecord(records[index], out var normalizedLine)
                || !IsValidStatusRecord(normalizedLine))
            {
                return ExternalSourceCheckoutTrust.Unverified;
            }

            if (IsOwnershipMarkerRecord(normalizedLine))
            {
                continue;
            }

            hasDirtyRecord = true;
        }

        return hasDirtyRecord
            ? ExternalSourceCheckoutTrust.Dirty
            : null;
    }

    private static bool TryNormalizeStatusRecord(string record, out string normalizedRecord)
    {
        var carriageReturnIndex = record.IndexOf('\r');
        if (carriageReturnIndex >= 0 && carriageReturnIndex != record.Length - 1)
        {
            normalizedRecord = string.Empty;
            return false;
        }

        normalizedRecord = carriageReturnIndex >= 0
            && carriageReturnIndex == record.Length - 1
            ? record[..^1]
            : record;
        return true;
    }

    private static bool IsValidStatusRecord(string record) =>
        record.Length >= 3
        && IsStatusCode(record[0])
        && IsStatusCode(record[1])
        && IsValidStatusPair(record[0], record[1])
        && record[2] is ' ';

    private static bool IsOwnershipMarkerRecord(string record) =>
        record.StartsWith(UntrackedStatusPrefix, StringComparison.Ordinal)
        && string.Equals(
            record[3..],
            ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
            StringComparison.Ordinal);

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
