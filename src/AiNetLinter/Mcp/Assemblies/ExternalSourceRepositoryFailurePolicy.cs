#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryFailurePolicy
{
    private const string SafeTransportLocation = "$repository";
    private const string SafeWarningSeverity = "warning";
    private const string SafeErrorSeverity = "error";

    internal static bool IsFileSystemException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException;

    internal static ExternalSourceProviderFailureKind ClassifyTransportException(
        Exception exception) =>
        exception switch
        {
            HttpRequestException => ExternalSourceProviderFailureKind.NetworkUnavailable,
            TimeoutException => ExternalSourceProviderFailureKind.Timeout,
            UnauthorizedAccessException => ExternalSourceProviderFailureKind.AccessDenied,
            _ => ExternalSourceProviderFailureKind.InvalidResponse,
        };

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
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed => code,
            _ => ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportFailed,
        };

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
            ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed =>
                "Der Repository-Checkout konnte nicht sicher bereinigt werden.",
            _ => "Die Repository-Akquisition ist fehlgeschlagen.",
        };
}
