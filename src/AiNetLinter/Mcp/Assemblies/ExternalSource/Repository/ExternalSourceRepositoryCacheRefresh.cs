#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

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
        using var readResultLifetime = readResult;
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
            return reservationFailure is null
                ? ExternalSourceRepositorySourcePolicy.CreateRefreshFailure(
                    ExternalSourceProviderFailureKind.InvalidResponse,
                    [CreateDiagnostic(
                        ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutPathInvalid,
                        "Der fällige Repository-Refresh konnte keinen sicheren Checkout reservieren.")],
                    readResult.Manifest.LoadedRevision)
                : ExternalSourceRepositorySourcePolicy.CreateRefreshFailure(
                    reservationFailure.FailureKind,
                    reservationFailure.Diagnostics,
                    readResult.Manifest.LoadedRevision);
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
            if (!ownership.TryCleanupOrQuarantine(
                    "Checkout konnte nach Refresh-Cancellation nicht sicher bereinigt werden.",
                    out var quarantine))
            {
                logger.Warning(
                    "Externer Repository-Refresh-Checkout konnte nach Cancellation nicht bereinigt werden. Status={Status} Code={Code}",
                    quarantine is null ? "quarantäne-fehlgeschlagen" : "quarantiniert",
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCleanupFailed);
            }

            throw;
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                    "Der fällige Repository-Refresh konnte nicht validiert werden.")],
                readResult.Manifest.LoadedRevision);
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
            return RefreshPreparation.Failed(ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutInvalid,
                    "Der reservierte Refresh-Checkout konnte vor dem Fetch nicht verifiziert werden.")],
                readResult.Manifest.LoadedRevision));
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
            return RefreshPreparation.Failed(ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                new ExternalSourceRepositoryFailureAfterCleanupParameters(
                    ownership,
                    transportResult.FailureKind,
                    transportResult.Diagnostics,
                    readResult.Manifest.LoadedRevision,
                    transportResult.CheckoutTrust)));
        }

        if (!ExternalSourceRepositorySourcePolicy.IsVerifiedTransport(transportResult))
        {
            return RefreshPreparation.Failed(ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                ExternalSourceProviderFailureKind.InvalidResponse,
                [CreateDiagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.RepositoryCheckoutUnverified,
                    "Der Repository-Transport hat keinen cleanen, verifizierten Checkout geliefert.")],
                readResult.Manifest.LoadedRevision));
        }

        var validation = validateCheckout(ownership, solutionPath, transportResult);
        return validation.IsValid
            ? RefreshPreparation.Succeeded(transportResult, validation)
            : RefreshPreparation.Failed(ExternalSourceRepositorySourcePolicy.FailureAfterCleanup(
                ownership,
                validation.FailureKind,
                validation.Diagnostics,
                readResult.Manifest.LoadedRevision));
    }

    private async Task<ExternalSourceRepositoryAcquisitionResult> PublishRefreshedCheckoutAsync(
        RefreshPublishParameters parameters,
        CancellationToken cancellationToken)
    {
        var checkout = new ExternalSourceCheckoutHandle(
            parameters.Ownership,
            parameters.ValidatedSolutionPath,
            parameters.TransportResult.LoadedRevision!,
            parameters.TransportResult.CheckoutAttestation);
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
            parameters.ReadResult,
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
        ExternalSourceRepositoryCacheReadResult readResult,
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

        return ExternalSourceRepositorySourcePolicy.CreateRefreshFailure(
            ExternalSourceProviderFailureKind.InvalidResponse,
            diagnostics,
            readResult.Manifest.LoadedRevision,
            publishResult.CheckoutTrust);
    }

    private Task<ExternalSourceRepositoryTransportResult> ExecuteFetchAsync(
        ExternalSourceMapping mapping,
        string checkoutPath,
        CancellationToken cancellationToken)
        => ExternalSourceRepositoryTransportExecution.ExecuteAsync(
            new(
                mapping,
                checkoutPath,
                cancellationToken,
                transport.FetchDefaultBranchAsync,
                "Die Repository-Aktualisierung ist fehlgeschlagen."));

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

        var reuse = cacheReuse.TryAcquire(mapping.Url, solutionPath, cancellationToken);
        return reuse is { IsAvailable: true } ? reuse : null;
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
