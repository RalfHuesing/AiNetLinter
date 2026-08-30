#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal static class ExternalSourceRepositoryCheckoutReservation
{
    private const string CheckoutDirectoryPrefix = "checkout-";
    private const int CheckoutPathCreationAttempts = 4;

    internal static bool TryCreate(
        string stagingRoot,
        out ExternalSourceCheckoutOwnership? ownership,
        out ExternalSourceRepositoryAcquisitionResult? failure)
    {
        ownership = null;
        failure = null;
        for (var attempt = 0; attempt < CheckoutPathCreationAttempts; attempt++)
        {
            var candidate = Path.Combine(
                stagingRoot,
                CheckoutDirectoryPrefix + Guid.NewGuid().ToString("N"));
            if (!IsSafeCandidate(stagingRoot, candidate) || !TryReserveDirectory(candidate))
            {
                continue;
            }

            if (!TryCreateOwnership(stagingRoot, candidate, out ownership))
            {
                failure = CreateFailure(!TryDeleteFreshReservation(candidate));
                return false;
            }

            if (ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership!))
            {
                return true;
            }

            var cleanupFailed = !ownership!.TryCleanup();
            ownership = null;
            failure = CreateFailure(cleanupFailed);
            return false;
        }

        failure = ExternalSourceRepositoryAcquisitionResult.Failure(
            ExternalSourceProviderFailureKind.InvalidResponse,
            [CreateDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid,
                "Es konnte kein freier Checkout-Pfad innerhalb der Staging-Wurzel reserviert werden.")]);
        return false;
    }

    private static bool IsSafeCandidate(string stagingRoot, string candidate) =>
        ExternalSourceRepositoryPathGuard.IsDescendantPath(stagingRoot, candidate)
            && !ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(stagingRoot);

    private static bool TryReserveDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return CreateDirectoryAtomically(path, IntPtr.Zero);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception)
            || exception is DllNotFoundException
            or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool TryCreateOwnership(
        string stagingRoot,
        string checkoutPath,
        out ExternalSourceCheckoutOwnership? ownership)
    {
        ownership = null;
        var ownershipId = Guid.NewGuid().ToString();
        var markerPath = Path.Combine(
            checkoutPath,
            ExternalSourceCheckoutOwnership.OwnershipMarkerFileName);
        try
        {
            using var marker = new FileStream(
                markerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            var ownershipBytes = Encoding.UTF8.GetBytes(ownershipId);
            marker.Write(ownershipBytes, 0, ownershipBytes.Length);
            marker.Flush(flushToDisk: true);
            ownership = new ExternalSourceCheckoutOwnership(stagingRoot, checkoutPath, ownershipId);
            return true;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool TryDeleteFreshReservation(string checkoutPath)
    {
        try
        {
            return !Directory.Exists(checkoutPath)
                || !ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(checkoutPath)
                    && !ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(checkoutPath)
                    && DeleteDirectory(checkoutPath);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool DeleteDirectory(string path)
    {
        Directory.Delete(path, recursive: true);
        return !Directory.Exists(path);
    }

    private static ExternalSourceRepositoryAcquisitionResult CreateFailure(bool cleanupFailed) =>
        cleanupFailed
            ? ExternalSourceRepositoryAcquisitionResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [
                    CreateDiagnostic(
                        ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid,
                        "Der reservierte Checkout-Pfad konnte nicht sicher vorbereitet werden."),
                    CreateDiagnostic(
                        ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
                        "Der eigene unvollständige Checkout konnte nicht vollständig bereinigt werden."),
                ])
            : ExternalSourceRepositoryAcquisitionResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid,
                    "Der reservierte Checkout-Pfad konnte nicht sicher vorbereitet werden.")]);

    private static ExternalSourceConfigurationDiagnostic CreateDiagnostic(
        string code,
        string message) =>
        ExternalSourceConfigurationDiagnostic.CreateError(
            code,
            message,
            nameof(ExternalSourceRepositoryCheckoutReservation),
            "$repository");

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryAtomically(
        string path,
        IntPtr securityAttributes);
}
