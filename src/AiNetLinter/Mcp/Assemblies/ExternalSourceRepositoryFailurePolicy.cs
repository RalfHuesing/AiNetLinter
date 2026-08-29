#nullable enable

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

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
        if (ContainsAny(output, "repository not found", "404", "not found"))
        {
            return ExternalSourceProviderFailureKind.RepositoryNotFound;
        }

        if (ContainsAny(
                output,
                "could not read Username",
                "terminal prompts disabled",
                "authentication required",
                "authentication failed",
                "http basic: access denied",
                "401"))
        {
            return hasCredential
                ? ExternalSourceProviderFailureKind.AccessDenied
                : ExternalSourceProviderFailureKind.AuthenticationRequired;
        }

        if (ContainsAny(output, "access denied", "permission denied", "forbidden", "403"))
        {
            return ExternalSourceProviderFailureKind.AccessDenied;
        }

        if (ContainsAny(
                output,
                "could not resolve host",
                "failed to connect",
                "connection refused",
                "network is unreachable",
                "unable to access",
                "connection timed out"))
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
            _ => ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed,
        };

    private static bool ContainsAny(string value, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
            _ => "Die Repository-Akquisition ist fehlgeschlagen.",
        };
}
