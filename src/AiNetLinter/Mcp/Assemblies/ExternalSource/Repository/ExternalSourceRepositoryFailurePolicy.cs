#nullable enable

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Http;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal static class ExternalSourceRepositoryFailurePolicy
{
    internal const int ErrorPrivilegeNotHeld = 1314;

    private const int Win32ErrorMask = 0xFFFF;
    private const string SafeTransportLocation = "$repository";
    private const string SafeWarningSeverity = "warning";
    private const string SafeErrorSeverity = "error";

    internal static bool IsFileSystemException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException;

    internal static bool IsPrivilegeNotHeld(Exception exception)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (exception is Win32Exception win32Exception)
        {
            return win32Exception.NativeErrorCode == ErrorPrivilegeNotHeld;
        }

        return exception is IOException or UnauthorizedAccessException
            && (exception.HResult & Win32ErrorMask) == ErrorPrivilegeNotHeld;
    }

    internal static ExternalSourceProviderFailureKind ClassifyTransportException(
        Exception exception)
    {
        if (IsPrivilegeNotHeld(exception))
        {
            return ExternalSourceProviderFailureKind.ProviderUnavailable;
        }

        return exception switch
        {
            HttpRequestException => ExternalSourceProviderFailureKind.NetworkUnavailable,
            TimeoutException => ExternalSourceProviderFailureKind.Timeout,
            UnauthorizedAccessException => ExternalSourceProviderFailureKind.AccessDenied,
            _ => ExternalSourceProviderFailureKind.InvalidResponse,
        };
    }

    internal static string GetTransportDiagnosticCode(Exception exception) =>
        IsPrivilegeNotHeld(exception)
            ? ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable
            : ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed;

    internal static ExternalSourceProviderFailureKind ClassifyGitProcessFailure(
        ExternalSourceGitProcessResult processResult,
        bool hasCredential)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        if (processResult.WasTimedOut)
        {
            return ExternalSourceProviderFailureKind.Timeout;
        }

        var output = processResult.StandardError + "\n" + processResult.StandardOutput;
        var httpStatus = ParseHttpStatus(output);
        if (httpStatus is HttpStatusCode.Unauthorized)
        {
            return hasCredential
                ? ExternalSourceProviderFailureKind.AccessDenied
                : ExternalSourceProviderFailureKind.AuthenticationRequired;
        }

        if (httpStatus is HttpStatusCode.Forbidden)
        {
            return ExternalSourceProviderFailureKind.AccessDenied;
        }

        if (httpStatus is HttpStatusCode.NotFound)
        {
            return ExternalSourceProviderFailureKind.RepositoryNotFound;
        }

        if (httpStatus is HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError)
        {
            return ExternalSourceProviderFailureKind.InvalidResponse;
        }

        if (HasAuthenticationEvidence(output))
        {
            return hasCredential
                ? ExternalSourceProviderFailureKind.AccessDenied
                : ExternalSourceProviderFailureKind.AuthenticationRequired;
        }

        if (HasStatuslessNetworkEvidence(output))
        {
            return ExternalSourceProviderFailureKind.NetworkUnavailable;
        }

        return ExternalSourceProviderFailureKind.InvalidResponse;
    }

    internal static string GetFailureDiagnosticCode(
        ExternalSourceProviderFailureKind failureKind) =>
        FailureKindToCode(failureKind);

    internal static ImmutableArray<ExternalSourceConfigurationDiagnostic>
        ProjectTransportDiagnostics(
            IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics,
            bool isAvailable,
            ExternalSourceProviderFailureKind failureKind)
    {
        var projected = new List<ExternalSourceConfigurationDiagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            projected.Add(diagnostic is null
                ? CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed,
                    isAvailable)
                : ProjectDiagnostic(diagnostic, isAvailable));
        }

        if (projected.Count == 0 && !isAvailable)
        {
            projected.Add(CreateDiagnostic(
                FailureKindToCode(failureKind),
                isAvailable));
        }

        return projected.ToImmutableArray();
    }

    private static ExternalSourceConfigurationDiagnostic ProjectDiagnostic(
        ExternalSourceConfigurationDiagnostic diagnostic,
        bool isAvailable)
    {
        var code = NormalizeCode(diagnostic.Code);
        return CreateDiagnostic(code, isAvailable);
    }

    private static ExternalSourceConfigurationDiagnostic CreateDiagnostic(
        string code,
        bool isAvailable) =>
        new(
            code,
            MessageForCode(code),
            isAvailable ? SafeWarningSeverity : SafeErrorSeverity,
            SafeTransportLocation);

    private static string NormalizeCode(string? code) =>
        code switch
        {
            ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable => code,
            ExternalSourceConfigurationDiagnosticCodes.AuthenticationRequired => code,
            ExternalSourceConfigurationDiagnosticCodes.AccessDenied => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryNotFound => code,
            ExternalSourceConfigurationDiagnosticCodes.NetworkUnavailable => code,
            ExternalSourceConfigurationDiagnosticCodes.Timeout => code,
            ExternalSourceConfigurationDiagnosticCodes.InvalidResponse => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryStagingRootInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionPathInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutDirty => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified => code,
            ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded => code,
            ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode => code,
            ExternalSourceRepositoryCacheContract.PublishCancelledDiagnosticCode => code,
            ExternalSourceRepositoryCacheContract.CurrentChangedDiagnosticCode => code,
            _ => ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed,
        };

    private static HttpStatusCode? ParseHttpStatus(string output)
    {
        var hasUnauthorized = false;
        var hasForbidden = false;
        var hasNotFound = false;
        var hasInvalidResponse = false;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseHttpStatusLine(line.TrimEnd('\r'), out var status))
            {
                continue;
            }

            hasUnauthorized |= status == HttpStatusCode.Unauthorized;
            hasForbidden |= status == HttpStatusCode.Forbidden;
            hasNotFound |= status == HttpStatusCode.NotFound;
            hasInvalidResponse |= status is HttpStatusCode.BadRequest
                or HttpStatusCode.InternalServerError;
        }

        return hasUnauthorized
            ? HttpStatusCode.Unauthorized
            : hasForbidden
                ? HttpStatusCode.Forbidden
                : hasNotFound
                    ? HttpStatusCode.NotFound
                    : hasInvalidResponse
                        ? HttpStatusCode.BadRequest
                        : null;
    }

    private static bool TryParseHttpStatusLine(string line, out HttpStatusCode status)
    {
        const string prefix = "fatal: unable to access '";
        const string marker = "': The requested URL returned error: ";
        status = 0;
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var markerIndex = line.IndexOf(marker, prefix.Length, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var statusText = line[(markerIndex + marker.Length)..].Trim();
        if (statusText.Length != 3
            || !IsAsciiDigit(statusText[0])
            || !IsAsciiDigit(statusText[1])
            || !IsAsciiDigit(statusText[2]))
        {
            return false;
        }

        status = (HttpStatusCode)((statusText[0] - '0') * 100
            + (statusText[1] - '0') * 10
            + statusText[2] - '0');
        return true;
    }

    private static bool HasAuthenticationEvidence(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalizedLine = line.TrimEnd('\r');
            if (normalizedLine.StartsWith(
                    "fatal: could not read Username for '",
                    StringComparison.OrdinalIgnoreCase)
                && normalizedLine.EndsWith(
                    "': terminal prompts disabled",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalizedLine.StartsWith(
                    "fatal: authentication failed for '",
                    StringComparison.OrdinalIgnoreCase)
                && normalizedLine.EndsWith("'.", StringComparison.Ordinal))
            {
                return true;
            }

            if (normalizedLine.StartsWith(
                    "fatal: authentication failed: ",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    normalizedLine,
                    "remote: HTTP Basic: Access denied",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStatuslessNetworkEvidence(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryReadGitReason(line.TrimEnd('\r'), out var reason))
            {
                continue;
            }

            if (reason.StartsWith("Could not resolve host", StringComparison.OrdinalIgnoreCase)
                || reason.StartsWith("Failed to connect", StringComparison.OrdinalIgnoreCase)
                || reason.StartsWith("Connection refused", StringComparison.OrdinalIgnoreCase)
                || reason.StartsWith("Network is unreachable", StringComparison.OrdinalIgnoreCase)
                || reason.StartsWith("Connection timed out", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadGitReason(string line, out string reason)
    {
        const string prefix = "fatal: unable to access '";
        reason = string.Empty;
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = line.IndexOf("': ", prefix.Length, StringComparison.OrdinalIgnoreCase);
        if (separatorIndex < 0)
        {
            return false;
        }

        reason = line[(separatorIndex + 3)..].Trim();
        return reason.Length > 0;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static string FailureKindToCode(ExternalSourceProviderFailureKind failureKind) =>
        failureKind switch
        {
            ExternalSourceProviderFailureKind.ProviderUnavailable =>
                ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable,
            ExternalSourceProviderFailureKind.AuthenticationRequired =>
                ExternalSourceConfigurationDiagnosticCodes.AuthenticationRequired,
            ExternalSourceProviderFailureKind.AccessDenied =>
                ExternalSourceConfigurationDiagnosticCodes.AccessDenied,
            ExternalSourceProviderFailureKind.RepositoryNotFound =>
                ExternalSourceConfigurationDiagnosticCodes.RepositoryNotFound,
            ExternalSourceProviderFailureKind.NetworkUnavailable =>
                ExternalSourceConfigurationDiagnosticCodes.NetworkUnavailable,
            ExternalSourceProviderFailureKind.Timeout =>
                ExternalSourceConfigurationDiagnosticCodes.Timeout,
            _ => ExternalSourceConfigurationDiagnosticCodes.InvalidResponse,
        };

    private static string MessageForCode(string code) =>
        code switch
        {
            ExternalSourceConfigurationDiagnosticCodes.ProviderUnavailable =>
                "Der externe Source-Provider ist nicht verfügbar.",
            ExternalSourceConfigurationDiagnosticCodes.AuthenticationRequired =>
                "Die Repository-Akquisition erfordert eine Authentifizierung.",
            ExternalSourceConfigurationDiagnosticCodes.AccessDenied =>
                "Der Zugriff auf das Repository wurde verweigert.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryNotFound =>
                "Das Repository wurde nicht gefunden.",
            ExternalSourceConfigurationDiagnosticCodes.NetworkUnavailable =>
                "Das Repository-Netzwerk ist nicht verfügbar.",
            ExternalSourceConfigurationDiagnosticCodes.Timeout =>
                "Die Repository-Akquisition hat das Zeitlimit überschritten.",
            ExternalSourceConfigurationDiagnosticCodes.InvalidResponse =>
                "Der Repository-Transport hat eine ungültige Antwort geliefert.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid =>
                "Die Repository-Zuordnung ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryStagingRootInvalid =>
                "Die kontrollierte Staging-Wurzel ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid =>
                "Der reservierte Checkout-Pfad ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid =>
                "Der Repository-Checkout ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionPathInvalid =>
                "Der Solution-Pfad ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid =>
                "Die konfigurierte Solution ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid =>
                "Das Repository-Transportergebnis ist ungültig.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable =>
                "Die Repository-Capability ist für diese Source nicht verfügbar.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed =>
                "Der Repository-Checkout konnte nicht sicher bereinigt werden.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutDirty =>
                "Der Repository-Checkout enthält nicht veröffentlichte Änderungen.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified =>
                "Der Repository-Checkout konnte nicht als sauber verifiziert werden.",
            ExternalSourceConfigurationDiagnosticCodes.RepositoryRefreshDegraded =>
                "Der letzte verifizierte Repository-Stand bleibt nur als Last-good-Nachweis verfügbar.",
            ExternalSourceRepositoryCacheContract.PublishFailedDiagnosticCode =>
                "Die Repository-Cachegeneration konnte nicht veröffentlicht werden.",
            ExternalSourceRepositoryCacheContract.PublishCancelledDiagnosticCode =>
                "Die Veröffentlichung der Repository-Cachegeneration wurde abgebrochen.",
            ExternalSourceRepositoryCacheContract.CurrentChangedDiagnosticCode =>
                "Der aktuelle Repository-Cache hat sich während der Veröffentlichung geändert.",
            _ => "Die Repository-Akquisition ist fehlgeschlagen.",
        };
}
