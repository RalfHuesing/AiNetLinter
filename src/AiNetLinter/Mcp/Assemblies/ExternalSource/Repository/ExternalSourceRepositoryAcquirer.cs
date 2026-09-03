#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Locking;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed partial class ExternalSourceRepositoryAcquirer : IExternalSourceRepositoryAcquirer
{
    private readonly IGiteaRepositoryTransport transport;
    private readonly string stagingRoot;
    private readonly ILogger logger;
    private readonly IExternalSourceRepositoryCacheWriter cacheWriter;
    private static readonly AssemblyArtifactFileLockRegistry CheckoutLocks = new("checkout.lock");
    private readonly ExternalSourceRepositoryCacheRefresh cacheRefresh;
    internal ExternalSourceRepositoryAcquirer(
        IGiteaRepositoryTransport transport,
        string stagingRoot,
        ILogger? logger = null,
        IExternalSourceRepositoryCacheWriter? cacheWriter = null,
        IExternalSourceRepositoryCacheReader? cacheReader = null,
        ExternalSourceRepositoryCacheRefreshPolicy? refreshPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        this.transport = transport;
        this.stagingRoot = CanonicalizeStagingRoot(stagingRoot)
            ?? throw new ArgumentException(
                "Die Staging-Wurzel muss ein absoluter, gültiger Pfad sein.",
                nameof(stagingRoot));
        this.logger = logger ?? Log.Logger;
        var cacheConstruction = ExternalSourceRepositoryCacheOptionsFactory.Create(
            ExternalSourceCacheOptions.Default);
        this.cacheWriter = cacheWriter ?? cacheConstruction.CreateWriter();
        var effectiveCacheReader = cacheReader ?? this.cacheWriter as IExternalSourceRepositoryCacheReader;
        var cacheReuse = new ExternalSourceRepositoryCacheReuse(
            this.stagingRoot,
            effectiveCacheReader,
            this.logger);
        // Gemeinsamer OS-Level-Lock: serialisiert Checkout-Erzeugungen desselben stagingRoot
        // sowohl im direkten Pfad (AcquireAsync) als auch im CacheRefresh-Pfad.
        cacheRefresh = new ExternalSourceRepositoryCacheRefresh(
            new ExternalSourceRepositoryCacheRefreshContext
            {
                Transport = transport,
                StagingRoot = this.stagingRoot,
                Reader = effectiveCacheReader,
                CacheReuse = cacheReuse,
                Logger = this.logger,
                Policy = refreshPolicy ?? cacheConstruction.CreateRefreshPolicy(),
                ValidateCheckout = ValidateCheckout,
                PublishCache = PublishCacheAsync,
            });
    }

    internal async Task<ExternalSourceRepositoryAcquisitionResult> AcquireAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (!TryValidateMapping(mapping, out var solutionPath, out var mappingFailure))
        {
            return mappingFailure!;
        }
        if (!TryPrepareStagingRoot(out var stagingFailure))
        {
            return stagingFailure!;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var cacheResult = await cacheRefresh.TryAcquireAsync(
            mapping,
            solutionPath!,
            cancellationToken).ConfigureAwait(false);
        if (cacheResult is not null)
        {
            return cacheResult;
        }

        // Exklusiver OS-Lock pro Repository-Schlüssel: verhindert parallele Erstbeschaffungen/Klone desselben Repositories.
        var lockDir = ExternalSourceRepositoryCacheKey.TryCreate(mapping.Url, solutionPath!, out var cacheKey)
            ? Path.Combine(stagingRoot, ".locks", cacheKey!.StableValue)
            : Path.Combine(stagingRoot, ".locks", "default");
        await using var checkoutLock = await CheckoutLocks.AcquireAsync(lockDir, cancellationToken).ConfigureAwait(false);
        if (checkoutLock.IsStalled)
        {
            return ExternalSourceRepositoryAcquisitionResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.CheckoutLockStall,
                    $"Ein Repository-Checkout-Lock h\u00e4ngt l\u00e4nger als {checkoutLock.StallThreshold?.TotalMinutes:0} Minuten.")]);
        }

        // Double-check Cache nach Lock-Erwerb (anderer Prozess hat ggf. inzwischen publiziert).
        var postLockCache = await cacheRefresh.TryAcquireAsync(mapping, solutionPath!, cancellationToken).ConfigureAwait(false);
        if (postLockCache is not null)
        {
            return postLockCache;
        }

        if (!ExternalSourceRepositoryCheckoutReservation.TryCreate(
                stagingRoot,
                out var ownership,
                out var checkoutFailure))
        {
            return checkoutFailure!;
        }
        return await AcquireReservedCheckoutAsync(
            mapping,
            solutionPath!,
            ownership!,
            cancellationToken).ConfigureAwait(false);
    }

    Task<ExternalSourceRepositoryAcquisitionResult> IExternalSourceRepositoryAcquirer.AcquireAsync(
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken) =>
        AcquireAsync(mapping, cancellationToken);

    private async Task<ExternalSourceRepositoryAcquisitionResult> AcquireReservedCheckoutAsync(
        ExternalSourceMapping mapping,
        string solutionPath,
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
            {
                return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                    ownership,
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    [CreateDiagnostic(
                        ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                        "Der reservierte Checkout konnte vor dem Transport nicht verifiziert werden.")]);
            }

            var transportResult = await ExecuteTransportAsync(
                mapping,
                ownership.CheckoutPath,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CompleteTransportResultAsync(
                ownership,
                solutionPath,
                transportResult,
                mapping,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!ownership.TryCleanup())
            {
                logger.Warning(
                    "Externer Repository-Checkout konnte nach Cancellation nicht bereinigt werden. Code={Code}",
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
            }

            throw;
        }
    }

    private async Task<ExternalSourceRepositoryAcquisitionResult> CompleteTransportResultAsync(
        ExternalSourceCheckoutOwnership ownership,
        string solutionPath,
        ExternalSourceRepositoryTransportResult? transportResult,
        ExternalSourceMapping mapping,
        CancellationToken cancellationToken)
    {
        if (transportResult is null)
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryTransportResultInvalid,
                    "Der Repository-Transport hat kein Ergebnis geliefert.")]);
        }

        if (!transportResult.IsAvailable)
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                new ExternalSourceRepositoryFailureAfterCleanupParameters(
                    ownership,
                    transportResult.FailureKind,
                    transportResult.Diagnostics,
                    null,
                    transportResult.CheckoutTrust));
        }

        var checkoutValidation = ValidateCheckout(
            ownership,
            solutionPath,
            transportResult);
        if (!checkoutValidation.IsValid)
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                checkoutValidation.FailureKind,
                checkoutValidation.Diagnostics);
        }

        var checkout = new ExternalSourceCheckoutHandle(
            ownership,
            checkoutValidation.SolutionPath!,
            transportResult.LoadedRevision!,
            transportResult.CheckoutAttestation);

        return await PublishCacheAndCreateResultAsync(
                mapping,
                solutionPath,
                ownership,
                checkout,
                transportResult,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ExternalSourceRepositoryAcquisitionResult> PublishCacheAndCreateResultAsync(
        ExternalSourceMapping mapping,
        string solutionPath,
        ExternalSourceCheckoutOwnership ownership,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceRepositoryTransportResult transportResult,
        CancellationToken cancellationToken)
    {
        if (!ExternalSourceRepositoryCacheKey.TryCreate(mapping.Url, solutionPath, out var cacheKey))
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid,
                    "Die validierte Repository-Identität konnte nicht erzeugt werden.")]);
        }

        var request = new ExternalSourceRepositoryCachePublishRequest
        {
            Mapping = mapping,
            Checkout = checkout,
            CheckoutOwnership = ownership,
            CacheKey = cacheKey!,
            SolutionPath = solutionPath,
            LoadedRevision = transportResult.LoadedRevision!,
        };
        var cacheResult = await PublishCacheAsync(request, cancellationToken).ConfigureAwait(false);
        if (cacheResult.FailureKind is ExternalSourceRepositoryCachePublishFailureKind.Cancelled
            && cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>(transportResult.Diagnostics);
        if (!cacheResult.Succeeded)
        {
            diagnostics.AddRange(cacheResult.Diagnostics);
        }

        if (cacheResult.FailureKind is ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource)
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                new ExternalSourceRepositoryFailureAfterCleanupParameters(
                    ownership,
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    diagnostics,
                    null,
                    cacheResult.CheckoutTrust));
        }

        return ExternalSourceRepositoryAcquisitionResult.Success(checkout, diagnostics);
    }

    private async Task<ExternalSourceRepositoryCachePublishResult> PublishCacheAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cacheWriter.PublishAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.WriteFailed);
        }
    }

    private async Task<ExternalSourceRepositoryTransportResult?> ExecuteTransportAsync(
        ExternalSourceMapping mapping,
        string checkoutPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await transport.CloneDefaultBranchAsync(
                mapping,
                checkoutPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ExternalSourceRepositoryTransportResult(
                isAvailable: false,
                loadedRevision: null,
                diagnostics: [CreateDiagnostic(
                    ExternalSourceRepositoryFailurePolicy.GetTransportDiagnosticCode(exception),
                    "Die Repository-Akquisition ist fehlgeschlagen.")],
                state: ExternalSourceRepositoryResultState.Create(
                    ExternalSourceRepositoryFailurePolicy.ClassifyTransportException(exception)));
        }
    }

    private static bool TryValidateMapping(
        ExternalSourceMapping mapping,
        out string? solutionPath,
        out ExternalSourceRepositoryAcquisitionResult? failure)
    {
        solutionPath = null;
        failure = null;
        if (!ExternalSourceUrlPolicy.TryNormalize(mapping.Url, out _))
        {
            failure = InvalidResult(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid,
                "Die Repository-Zuordnung enthält keine unterstützte HTTP(S)-URL.");
            return false;
        }

        if (!ExternalSourceRepositoryCacheKey.TryNormalizeSolutionPath(
                mapping.SolutionPath,
                out var normalizedSolutionPath))
        {
            failure = InvalidResult(
                ExternalSourceConfigurationDiagnosticCodes.RepositorySolutionPathInvalid,
                "Der konfigurierte Solution-Pfad ist kein sicherer repository-relativer .sln- oder .slnx-Pfad.");
            return false;
        }

        solutionPath = normalizedSolutionPath!.Replace(
            '/',
            Path.DirectorySeparatorChar);

        foreach (var assembly in mapping.Assemblies)
        {
            if (string.IsNullOrWhiteSpace(assembly))
            {
                failure = InvalidResult(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid,
                    "Die Repository-Zuordnung enthält einen leeren Assembly-Alias.");
                return false;
            }
        }

        if (mapping.Assemblies.IsDefaultOrEmpty)
        {
            failure = InvalidResult(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryMappingInvalid,
                "Die Repository-Zuordnung enthält keine Assembly-Aliase.");
            return false;
        }

        return true;
    }

    private bool TryPrepareStagingRoot(out ExternalSourceRepositoryAcquisitionResult? failure)
    {
        failure = null;
        try
        {
            if (File.Exists(stagingRoot)
                || Directory.Exists(stagingRoot)
                    && ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(stagingRoot))
            {
                failure = InvalidResult(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryStagingRootInvalid,
                    "Die kontrollierte Staging-Wurzel ist keine sichere Verzeichniswurzel.");
                return false;
            }

            Directory.CreateDirectory(stagingRoot);
            if (!Directory.Exists(stagingRoot)
                || ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(stagingRoot))
            {
                failure = InvalidResult(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryStagingRootInvalid,
                    "Die kontrollierte Staging-Wurzel konnte nicht sicher verifiziert werden.");
                return false;
            }

            return true;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            failure = InvalidResult(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryStagingRootInvalid,
                "Die kontrollierte Staging-Wurzel konnte nicht vorbereitet werden.");
            return false;
        }
    }
}
