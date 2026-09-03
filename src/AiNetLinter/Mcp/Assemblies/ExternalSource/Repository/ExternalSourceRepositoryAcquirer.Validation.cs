#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed partial class ExternalSourceRepositoryAcquirer
{
    private CheckoutValidationResult ValidateCheckout(
        ExternalSourceCheckoutOwnership ownership,
        string solutionPath,
        ExternalSourceRepositoryTransportResult transportResult)
    {
        if (!ExternalSourceRepositorySourcePolicy.IsVerifiedTransport(transportResult))
        {
            return CheckoutValidationResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified,
                    "Der Repository-Transport hat keinen cleanen, verifizierten Checkout geliefert.")]);
        }

        if (TryGetCheckoutPathFailure(ownership, out var pathFailure))
        {
            return pathFailure!;
        }

        string solutionAbsolutePath;
        try
        {
            solutionAbsolutePath = Path.GetFullPath(Path.Combine(ownership.CheckoutPath, solutionPath));
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return CheckoutValidationResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionPathInvalid,
                    "Der Solution-Pfad konnte nicht aufgelöst werden.")]);
        }

        if (!ExternalSourceRepositoryPathGuard.IsDescendantPath(
                ownership.CheckoutPath,
                solutionAbsolutePath)
            || !ExternalSourceRepositoryPathGuard.IsDescendantPath(
                stagingRoot,
                solutionAbsolutePath)
            || !File.Exists(solutionAbsolutePath)
            || ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(solutionAbsolutePath))
        {
            return CheckoutValidationResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionInvalid,
                    "Die konfigurierte Solution liegt nicht als sichere Datei im Checkout vor.")]);
        }

        return CheckoutValidationResult.Success(solutionAbsolutePath);
    }

    private static bool TryGetCheckoutPathFailure(
        ExternalSourceCheckoutOwnership ownership,
        out CheckoutValidationResult? failure)
    {
        failure = null;
        if (ExternalSourceRepositoryPathGuard.ContainsActualReparsePointOnPath(
                ownership.CheckoutPath)
            || ExternalSourceRepositoryPathGuard.ContainsActualReparsePointInTree(
                ownership.CheckoutPath))
        {
            failure = CheckoutValidationResult.Failure(
                ExternalSourceProviderFailureKind.ProviderUnavailable,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCapabilityUnavailable,
                    "Der Repository-Checkout benötigt eine nicht verfügbare lokale Capability.")]);
            return true;
        }

        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership)
            || ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(ownership.CheckoutPath))
        {
            failure = CheckoutValidationResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                    "Der Repository-Transport hat keinen sicheren Checkout innerhalb der Staging-Wurzel erzeugt.")]);
            return true;
        }

        return false;
    }

    private static ExternalSourceRepositoryAcquisitionResult InvalidResult(
        string code,
        string message) =>
        ExternalSourceRepositoryAcquisitionResult.Failure(
            ExternalSourceProviderFailureKind.InvalidResponse,
            [CreateDiagnostic(code, message)]);

    private static ExternalSourceConfigurationDiagnostic CreateDiagnostic(
        string code,
        string message) =>
        ExternalSourceConfigurationDiagnostic.CreateError(
            code,
            message,
            nameof(ExternalSourceRepositoryAcquirer),
            "$repository");

    private static string? CanonicalizeStagingRoot(string value)
        => ExternalSourceRepositoryCacheContract.TryCanonicalizeAbsoluteRoot(value);

    internal static bool IsReparsePointAttribute(FileAttributes attributes) =>
        ExternalSourceRepositoryPathGuard.IsReparsePointAttribute(attributes);
}
