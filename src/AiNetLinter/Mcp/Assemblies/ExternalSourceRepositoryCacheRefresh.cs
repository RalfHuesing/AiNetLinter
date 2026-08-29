#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceRepositoryCacheRefreshContext
{
    internal IGiteaRepositoryTransport Transport { get; init; } = null!;

    internal string StagingRoot { get; init; } = string.Empty;

    internal IExternalSourceRepositoryCacheReader? Reader { get; init; }

    internal ExternalSourceRepositoryCacheReuse CacheReuse { get; init; } = null!;

    internal ILogger Logger { get; init; } = null!;

    internal ExternalSourceRepositoryCacheRefreshPolicy Policy { get; init; } = null!;

    internal Func<ExternalSourceCheckoutOwnership, string, ExternalSourceRepositoryTransportResult, CheckoutValidationResult> ValidateCheckout { get; init; } = null!;

    internal Func<ExternalSourceRepositoryCachePublishRequest, CancellationToken, Task<ExternalSourceRepositoryCachePublishResult>> PublishCache { get; init; } = null!;
}

internal sealed class ExternalSourceRepositoryCacheRefresh
{
    private readonly IGiteaRepositoryTransport transport;
    private readonly string stagingRoot;
    private readonly IExternalSourceRepositoryCacheReader? reader;
    private readonly ExternalSourceRepositoryCacheReuse cacheReuse;
    private readonly ILogger logger;
    private readonly ExternalSourceRepositoryCacheRefreshPolicy policy;
    private readonly Func<ExternalSourceCheckoutOwnership, string, ExternalSourceRepositoryTransportResult, CheckoutValidationResult> validateCheckout;
    private readonly Func<ExternalSourceRepositoryCachePublishRequest, CancellationToken, Task<ExternalSourceRepositoryCachePublishResult>> publishCache;

    internal ExternalSourceRepositoryCacheRefresh(ExternalSourceRepositoryCacheRefreshContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        transport = context.Transport;
        stagingRoot = context.StagingRoot;
        reader = context.Reader;
        cacheReuse = context.CacheReuse;
        logger = context.Logger;
        policy = context.Policy;
        validateCheckout = context.ValidateCheckout;
        publishCache = context.PublishCache;
    }

    internal async Task<ExternalSourceRepositoryAcquisitionResult?> TryAcquireAsync(
        ExternalSourceMapping mapping,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        if (reader is null
            || !ExternalSourceRepositoryCacheKey.TryCreate(mapping.Url, solutionPath, out var key))
        {
            return null;
        }

        ExternalSourceRepositoryCacheReadResult? readResult;
        try
        {
            if (!reader.TryReadCurrent(key!, out readResult, out _)
                || readResult is null)
            {
                return null;
            }
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!policy.IsStale(readResult.Manifest))
        {
            return cacheReuse.TryAcquire(mapping.Url, solutionPath, cancellationToken);
        }

        if (!ExternalSourceRepositoryCheckoutReservation.TryCreate(
                stagingRoot,
                out var ownership,
                out var reservationFailure)
            || ownership is null)
        {
            return reservationFailure ?? ExternalSourceRepositoryAcquisitionResult.Failure(
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid,
                    "Der fällige Repository-Refresh konnte keinen sicheren Checkout reservieren.")]);
        }

        return await RefreshReservedCheckoutAsync(
                mapping,
                solutionPath,
                key!,
                readResult,
                ownership,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ExternalSourceRepositoryAcquisitionResult> RefreshReservedCheckoutAsync(
        ExternalSourceMapping mapping,
        string solutionPath,
        ExternalSourceRepositoryCacheKey key,
        ExternalSourceRepositoryCacheReadResult readResult,
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken)
    {
        try
        {
            var preparation = await PrepareRefreshCheckoutAsync(
                    mapping,
                    solutionPath,
                    readResult,
                    ownership,
                    cancellationToken)
                .ConfigureAwait(false);
            if (preparation.Failure is not null)
            {
                return preparation.Failure;
            }

            return await PublishRefreshedCheckoutAsync(
                    new RefreshPublishParameters
                    {
                        Mapping = mapping,
                        SolutionPath = solutionPath,
                        Key = key,
                        ReadResult = readResult,
                        Ownership = ownership,
                        ValidatedSolutionPath = preparation.Validation!.SolutionPath!,
                        TransportResult = preparation.TransportResult!,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!ownership.TryCleanup())
            {
                logger.Warning(
                    "Externer Repository-Refresh-Checkout konnte nach Cancellation nicht bereinigt werden. Code={Code}",
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
            }

            throw;
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return ExternalSourceRepositoryAcquirer.FailAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                    "Der fällige Repository-Refresh konnte nicht validiert werden.")]);
        }
    }

    private async Task<RefreshPreparation> PrepareRefreshCheckoutAsync(
        ExternalSourceMapping mapping,
        string solutionPath,
        ExternalSourceRepositoryCacheReadResult readResult,
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken)
    {
        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
        {
            return RefreshPreparation.Failed(ExternalSourceRepositoryAcquirer.FailAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                    "Der reservierte Refresh-Checkout konnte vor dem Fetch nicht verifiziert werden.")]));
        }

        _ = ExternalSourceRepositoryCacheMaterializer.Materialize(
            readResult,
            ownership,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var transportResult = await ExecuteFetchAsync(
                mapping,
                ownership.CheckoutPath,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!transportResult.IsAvailable)
        {
            return RefreshPreparation.Failed(ExternalSourceRepositoryAcquirer.FailAfterCleanup(
                ownership,
                transportResult.FailureKind,
                transportResult.Diagnostics));
        }

        var validation = validateCheckout(ownership, solutionPath, transportResult);
        return validation.IsValid
            ? RefreshPreparation.Succeeded(transportResult, validation)
            : RefreshPreparation.Failed(ExternalSourceRepositoryAcquirer.FailAfterCleanup(
                ownership,
                validation.FailureKind,
                validation.Diagnostics));
    }

    private async Task<ExternalSourceRepositoryAcquisitionResult> PublishRefreshedCheckoutAsync(
        RefreshPublishParameters parameters,
        CancellationToken cancellationToken)
    {
        var checkout = new ExternalSourceCheckoutHandle(
            parameters.Ownership,
            parameters.ValidatedSolutionPath,
            parameters.TransportResult.LoadedRevision!);
        var publishResult = await publishCache(
                CreatePublishRequest(parameters, checkout),
                cancellationToken)
            .ConfigureAwait(false);
        if (publishResult.Succeeded)
        {
            return ExternalSourceRepositoryAcquisitionResult.Success(
                checkout,
                parameters.TransportResult.Diagnostics);
        }

        return CompleteFailedPublish(
            parameters.Mapping,
            parameters.SolutionPath,
            checkout,
            publishResult,
            cancellationToken);
    }

    private static ExternalSourceRepositoryCachePublishRequest CreatePublishRequest(
        RefreshPublishParameters parameters,
        ExternalSourceCheckoutHandle checkout) =>
        new()
        {
            Mapping = parameters.Mapping,
            Checkout = checkout,
            CheckoutOwnership = parameters.Ownership,
            CacheKey = parameters.Key,
            SolutionPath = parameters.SolutionPath,
            LoadedRevision = parameters.TransportResult.LoadedRevision!,
            ExpectedCurrentGeneration = parameters.ReadResult.Manifest.GenerationName,
        };

    private ExternalSourceRepositoryAcquisitionResult CompleteFailedPublish(
        ExternalSourceMapping mapping,
        string solutionPath,
        ExternalSourceCheckoutHandle checkout,
        ExternalSourceRepositoryCachePublishResult publishResult,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>(publishResult.Diagnostics);
        checkout.Dispose();
        var cleanupFailed = checkout.CleanupState is ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed;
        if (cleanupFailed)
        {
            diagnostics.Add(CreateDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed,
                "Der eigene Refresh-Checkout konnte nicht vollständig bereinigt werden."));
        }

        if (publishResult.FailureKind is ExternalSourceRepositoryCachePublishFailureKind.Cancelled
            && cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (publishResult.FailureKind is ExternalSourceRepositoryCachePublishFailureKind.CurrentChanged
            && !cleanupFailed)
        {
            var currentResult = TryReuseFreshCurrentAfterRace(
                mapping,
                solutionPath,
                cancellationToken);
            if (currentResult is not null)
            {
                return currentResult;
            }
        }

        return ExternalSourceRepositoryAcquisitionResult.Failure(
            ExternalSourceProviderFailureKind.InvalidResponse,
            diagnostics);
    }

    private async Task<ExternalSourceRepositoryTransportResult> ExecuteFetchAsync(
        ExternalSourceMapping mapping,
        string checkoutPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await transport.FetchDefaultBranchAsync(
                    mapping,
                    checkoutPath,
                    cancellationToken)
                .ConfigureAwait(false);
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
                    "Die Repository-Aktualisierung ist fehlgeschlagen.")],
                failureKind: ExternalSourceRepositoryFailurePolicy.ClassifyTransportException(exception));
        }
    }

    private ExternalSourceRepositoryAcquisitionResult? TryReuseFreshCurrentAfterRace(
        ExternalSourceMapping mapping,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        if (reader is null
            || !ExternalSourceRepositoryCacheKey.TryCreate(mapping.Url, solutionPath, out var key)
            || !reader.TryReadCurrent(key!, out var current, out _)
            || current is null
            || policy.IsStale(current.Manifest))
        {
            return null;
        }

        return cacheReuse.TryAcquire(mapping.Url, solutionPath, cancellationToken);
    }

    private static ExternalSourceConfigurationDiagnostic CreateDiagnostic(
        string code,
        string message) =>
        ExternalSourceConfigurationDiagnostic.CreateError(
            code,
            message,
            nameof(ExternalSourceRepositoryCacheRefresh),
            "$repository");

    private sealed class RefreshPreparation
    {
        internal ExternalSourceRepositoryTransportResult? TransportResult { get; private init; }

        internal CheckoutValidationResult? Validation { get; private init; }

        internal ExternalSourceRepositoryAcquisitionResult? Failure { get; private init; }

        internal static RefreshPreparation Failed(ExternalSourceRepositoryAcquisitionResult failure) =>
            new() { Failure = failure };

        internal static RefreshPreparation Succeeded(
            ExternalSourceRepositoryTransportResult transportResult,
            CheckoutValidationResult validation) =>
            new() { TransportResult = transportResult, Validation = validation };
    }

    private sealed class RefreshPublishParameters
    {
        internal ExternalSourceMapping Mapping { get; init; } = null!;

        internal string SolutionPath { get; init; } = string.Empty;

        internal ExternalSourceRepositoryCacheKey Key { get; init; } = null!;

        internal ExternalSourceRepositoryCacheReadResult ReadResult { get; init; } = null!;

        internal ExternalSourceCheckoutOwnership Ownership { get; init; } = null!;

        internal string ValidatedSolutionPath { get; init; } = string.Empty;

        internal ExternalSourceRepositoryTransportResult TransportResult { get; init; } = null!;
    }

}
