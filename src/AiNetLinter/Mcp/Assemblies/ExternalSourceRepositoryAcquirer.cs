#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceRepositoryAcquirer : IExternalSourceRepositoryAcquirer
{
    private readonly IGiteaRepositoryTransport transport;
    private readonly string stagingRoot;
    private readonly ILogger logger;
    private readonly IExternalSourceRepositoryCacheWriter cacheWriter;
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
                ownership,
                transportResult.FailureKind,
                transportResult.Diagnostics);
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
            transportResult.LoadedRevision!);

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
        if (!ExternalSourceRepositoryUrlPolicy.TryNormalize(mapping.Url, out _))
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
