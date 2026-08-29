#nullable enable

using System;
using System.IO;
using System.Threading;
using AiNetLinter.Configuration;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceRepositoryCacheReuse
{
    private readonly string stagingRoot;
    private readonly IExternalSourceRepositoryCacheReader? reader;
    private readonly ILogger logger;

    internal ExternalSourceRepositoryCacheReuse(
        string stagingRoot,
        IExternalSourceRepositoryCacheReader? reader,
        ILogger logger)
    {
        this.stagingRoot = stagingRoot;
        this.reader = reader;
        this.logger = logger;
    }

    internal ExternalSourceRepositoryAcquisitionResult? TryAcquire(
        string url,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        var cacheReader = reader;
        if (cacheReader is null
            || !ExternalSourceRepositoryCacheKey.TryCreate(url, solutionPath, out var key))
        {
            return null;
        }

        if (!TryAcquire(
                key!,
                cancellationToken,
                out var checkout,
                out var cleanupFailed))
        {
            return cleanupFailed
                ? ExternalSourceRepositoryAcquisitionResult.Failure(
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    [ExternalSourceConfigurationDiagnostic.CreateError(
                        ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
                        "Der eigene Cache-Checkout konnte nicht vollständig bereinigt werden.",
                        nameof(ExternalSourceRepositoryCacheReuse),
                        "$repository")])
                : null;
        }

        return ExternalSourceRepositoryAcquisitionResult.Success(checkout!, []);
    }

    private bool TryAcquire(
        ExternalSourceRepositoryCacheKey key,
        CancellationToken cancellationToken,
        out ExternalSourceCheckoutHandle? checkout,
        out bool cleanupFailed)
    {
        checkout = null;
        cleanupFailed = false;
        ExternalSourceCheckoutOwnership? ownership = null;
        try
        {
            if (!reader!.TryReadCurrent(key, out var readResult, out _)
                || readResult is null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!ExternalSourceRepositoryCheckoutReservation.TryCreate(
                    stagingRoot,
                    out ownership,
                    out _)
                || ownership is null)
            {
                return false;
            }

            var solutionPath = ExternalSourceRepositoryCacheMaterializer.Materialize(
                readResult,
                ownership,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            checkout = new ExternalSourceCheckoutHandle(
                ownership,
                solutionPath,
                readResult.Manifest.LoadedRevision);
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!Cleanup(ownership))
            {
                logger.Warning(
                    "Externer Repository-Cache-Checkout konnte nach Cancellation nicht bereinigt werden. Code={Code}",
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
            }

            throw;
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            cleanupFailed = !Cleanup(ownership);
            if (cleanupFailed)
            {
                logger.Warning(
                    "Externer Repository-Cache-Checkout konnte nach Reuse-Fehler nicht bereinigt werden. Code={Code}",
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
            }

            return false;
        }
    }

    private static bool Cleanup(ExternalSourceCheckoutOwnership? ownership) =>
        ownership?.TryCleanup() ?? true;
}
