#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Locking;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal interface IExternalSourceRepositoryCacheWriter
{
    Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed partial class LocalExternalSourceRepositoryCacheWriter :
    IExternalSourceRepositoryCacheWriter,
    IExternalSourceRepositoryCacheReader
{
    private static readonly ExternalSourceRepositoryCacheKeyLockRegistry Locks = new();
    private static readonly AssemblyArtifactFileLockRegistry ProcessLocks = new("cache.lock");
    private readonly string cacheRoot;

    internal LocalExternalSourceRepositoryCacheWriter(string? cacheRoot = null)
    {
        var configuredRoot = cacheRoot
            ?? ExternalSourceRepositoryCacheOptionsFactory.Create(
                ExternalSourceCacheOptions.Default).SourceRoot;
        this.cacheRoot = ExternalSourceRepositoryCacheContract.TryCanonicalizeAbsoluteRoot(configuredRoot)
            ?? throw new ArgumentException(
                "Die Cache-Wurzel muss ein absoluter, gültiger Pfad sein.",
                nameof(cacheRoot));
    }

    internal string GetEntryDirectory(ExternalSourceRepositoryCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return System.IO.Path.Combine(cacheRoot, key.StableValue);
    }

    internal static int ActiveLockCount => Locks.Count;

    public Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken = default)
        => PublishAsyncCore(request, cancellationToken, null);

    internal Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken,
        ExternalSourceRepositoryCachePublishTestSeam? testSeam)
        => PublishAsyncCore(request, cancellationToken, testSeam);

    private async Task<ExternalSourceRepositoryCachePublishResult> PublishAsyncCore(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken,
        ExternalSourceRepositoryCachePublishTestSeam? testSeam)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryValidateRequest(request, out var key, out var failure))
        {
            return failure!;
        }

        var context = CreatePublishContext(request, key!);
        var published = false;
        ExternalSourceRepositoryCacheKeyLockLease? lockLease = null;
        AssemblyArtifactFileLockLease? processLock = null;
        ExternalSourceCheckoutMaterializationUse? materializationUse = null;
        try
        {
            var acquisition = await AcquirePublishResourcesAsync(context, cancellationToken).ConfigureAwait(false);
            lockLease = acquisition.LockLease;
            processLock = acquisition.ProcessLock;
            materializationUse = acquisition.MaterializationUse;
            if (acquisition.Failure is not null)
            {
                return acquisition.Failure;
            }

            var result = await PublishGeneration(
                    context,
                    cancellationToken,
                    testSeam)
                .ConfigureAwait(false);
            published = result.Succeeded;
            return result;
        }
        catch (OperationCanceledException)
        {
            return CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.Cancelled);
        }
        catch (ExternalSourceRepositoryCacheUnsafeSourceException exception)
        {
            return CreateFailure(
                ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource,
                exception.CheckoutTrust);
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.WriteFailed);
        }
        finally
        {
            await ExternalSourceRepositoryCachePublishLifecycle.FinalizeAndReleaseSourceAsync(
                    () => FinalizePublishAsync(
                        context,
                        published,
                        lockLease,
                        processLock,
                        testSeam?.AfterLeaseReleasedAsync),
                    materializationUse)
                .ConfigureAwait(false);
        }
    }

    private async Task<(ExternalSourceRepositoryCacheKeyLockLease? LockLease, AssemblyArtifactFileLockLease? ProcessLock, ExternalSourceCheckoutMaterializationUse? MaterializationUse, ExternalSourceRepositoryCachePublishResult? Failure)> AcquirePublishResourcesAsync(
        PublishContext context,
        CancellationToken cancellationToken)
    {
        var lockLease = await AcquireLockAsync(context.EntryDirectory, cancellationToken).ConfigureAwait(false);
        var processLock = await ProcessLocks.AcquireAsync(context.EntryDirectory, cancellationToken).ConfigureAwait(false);
        if (processLock.IsStalled)
        {
            return (lockLease, processLock, null, CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.WriteFailed));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var materializationUse = context.Request.Checkout.TryAcquireMaterializationUse(cancellationToken);
        return materializationUse is null
            ? (lockLease, processLock, null, CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource))
            : (lockLease, processLock, materializationUse, null);
    }

    private PublishContext CreatePublishContext(
        ExternalSourceRepositoryCachePublishRequest request,
        ExternalSourceRepositoryCacheKey key)
    {
        var entryDirectory = GetEntryDirectory(key);
        var generationName = ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix
            + Guid.NewGuid().ToString("N");
        return new PublishContext
        {
            Request = request,
            Key = key,
            EntryDirectory = entryDirectory,
            GenerationName = generationName,
            GenerationDirectory = System.IO.Path.Combine(entryDirectory, generationName),
        };
    }

    private async Task<ExternalSourceRepositoryCachePublishResult> PublishGeneration(
        PublishContext context,
        CancellationToken cancellationToken,
        ExternalSourceRepositoryCachePublishTestSeam? testSeam)
    {
        ExternalSourceRepositoryCacheStorage.PrepareDirectory(cacheRoot);
        ExternalSourceRepositoryCacheStorage.PrepareEntryDirectory(
            cacheRoot,
            context.EntryDirectory);
        await VerifyCheckoutAttestationAsync(context.Request, cancellationToken)
            .ConfigureAwait(false);
        ExternalSourceRepositoryCacheStorage.ValidateSourceCheckout(
            context.Request,
            context.Key);
        if (!TryPrepareCurrentGeneration(context, out var currentFailure))
        {
            return currentFailure!;
        }

        System.IO.Directory.CreateDirectory(context.GenerationDirectory);
        WriteGeneration(
            context.Request,
            context.Key,
            context.GenerationDirectory,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await InvokeTestHookAsync(testSeam?.BeforePointerPublishedAsync).ConfigureAwait(false);
        await VerifyCheckoutAttestationAsync(context.Request, cancellationToken)
            .ConfigureAwait(false);
        if (!ExternalSourceRepositoryCacheStorage.TryPublishPointer(
                context.EntryDirectory,
                context.GenerationName,
                context.Request.ExpectedCurrentGeneration ?? context.PreviousGeneration,
                out var pointerFailure))
        {
            return pointerFailure!;
        }

        context.PointerPublished = true;
        await InvokeTestHookAsync(testSeam?.AfterPointerPublishedAsync).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await VerifyCheckoutAttestationAsync(context.Request, cancellationToken)
            .ConfigureAwait(false);
        if (!TryValidatePublishedGeneration(
                context.Request,
                context.Key,
                context.EntryDirectory,
                context.GenerationName))
        {
            return ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.ManifestInvalid);
        }

        return ExternalSourceRepositoryCachePublishResult.Success(
            context.Key,
            context.GenerationName,
            context.GenerationDirectory);
    }

    private bool TryPrepareCurrentGeneration(
        PublishContext context,
        out ExternalSourceRepositoryCachePublishResult? failure)
    {
        var hasCurrent = TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = context.Key,
                EntryDirectory = context.EntryDirectory,
            },
            out var previousCurrent,
            out _);
        context.PreviousGeneration = hasCurrent
            ? previousCurrent!.Manifest.GenerationName
            : null;
        failure = null;
        if (context.Request.ExpectedCurrentGeneration is not null
            && !string.Equals(
                context.PreviousGeneration,
                context.Request.ExpectedCurrentGeneration,
                StringComparison.Ordinal))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.CurrentChanged);
            return false;
        }

        return true;
    }

    private void WriteGeneration(
        ExternalSourceRepositoryCachePublishRequest request,
        ExternalSourceRepositoryCacheKey key,
        string generationDirectory,
        CancellationToken cancellationToken)
    {
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(generationDirectory);
        var files = ExternalSourceRepositoryCacheStorage.CopySource(
            request.CheckoutOwnership.CheckoutPath,
            generationDirectory,
            cancellationToken);
        var manifest = new ExternalSourceRepositoryCacheManifest(
            key.SchemaVersion,
            key.StableValue,
            key.CanonicalRepositoryUrl,
            key.SolutionPath,
            request.LoadedRevision,
            System.IO.Path.GetFileName(generationDirectory),
            DateTime.UtcNow,
            files);
        ExternalSourceRepositoryCacheMetadataStorage.WriteManifest(generationDirectory, manifest);
        ExternalSourceRepositoryCacheMetadataStorage.WriteInventory(
            generationDirectory,
            key,
            manifest.GenerationName,
            files);
        ReadGeneration(new ExternalSourceRepositoryCacheReadRequest
        {
            Key = key,
            EntryDirectory = System.IO.Path.GetDirectoryName(generationDirectory)!,
            ExpectedRevision = request.LoadedRevision,
            ExpectedSolutionPath = key.SolutionPath,
        }, manifest.GenerationName);
    }

    private static bool TryValidatePublishedGeneration(
        ExternalSourceRepositoryCachePublishRequest request,
        ExternalSourceRepositoryCacheKey key,
        string entryDirectory,
        string generationName)
    {
        try
        {
            ReadGeneration(new ExternalSourceRepositoryCacheReadRequest
            {
                Key = key,
                EntryDirectory = entryDirectory,
                ExpectedRevision = request.LoadedRevision,
                ExpectedSolutionPath = key.SolutionPath,
            }, generationName);
            return true;
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return false;
        }
    }

    internal bool TryReadCurrent(
        ExternalSourceRepositoryCacheKey key,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic) =>
        TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = key,
                EntryDirectory = GetEntryDirectory(key),
            },
            out result,
            out diagnostic);

    bool IExternalSourceRepositoryCacheReader.TryReadCurrent(
        ExternalSourceRepositoryCacheKey key,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic) =>
        TryReadCurrent(key, out result, out diagnostic);

    internal bool TryReadCurrent(
        ExternalSourceRepositoryCacheReadRequest request,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic) =>
        TryReadCurrentCore(
            request,
            out result,
            out diagnostic);

    private static bool TryReadCurrentCore(
        ExternalSourceRepositoryCacheReadRequest request,
        out ExternalSourceRepositoryCacheReadResult? result,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(request);
        result = null;
        diagnostic = null;
        try
        {
            return ExternalSourceRepositoryCacheReader.TryReadCurrent(request, out result);
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            diagnostic = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.ManifestInvalid).Diagnostics[0];
            return false;
        }
    }

    private static ExternalSourceRepositoryCacheReadResult ReadGeneration(
        ExternalSourceRepositoryCacheReadRequest request,
        string generationName) =>
        ExternalSourceRepositoryCacheReader.ReadGeneration(request, generationName);

    private static async Task<ExternalSourceRepositoryCacheKeyLockLease> AcquireLockAsync(
        string entryDirectory,
        CancellationToken cancellationToken)
    {
        var lockKey = System.IO.Path.GetFullPath(entryDirectory);
        return await Locks.AcquireAsync(lockKey, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryValidateRequest(
        ExternalSourceRepositoryCachePublishRequest request,
        out ExternalSourceRepositoryCacheKey? key,
        out ExternalSourceRepositoryCachePublishResult? failure)
    {
        key = null;
        failure = null;
        if (!TryValidateRequestIdentity(request, out key))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.InvalidRequest);
            return false;
        }

        if (request.Checkout.IsDisposed
            || !string.Equals(
                request.CheckoutOwnership.CheckoutPath,
                request.Checkout.CheckoutPath,
                StringComparison.OrdinalIgnoreCase))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource);
            return false;
        }

        if (request.Checkout.CheckoutAttestation is null
            || !string.Equals(
                request.Checkout.CheckoutAttestation.ExpectedRevision,
                request.LoadedRevision,
                StringComparison.Ordinal))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource);
            return false;
        }

        return true;
    }

    private static bool TryValidateRequestIdentity(
        ExternalSourceRepositoryCachePublishRequest request,
        out ExternalSourceRepositoryCacheKey? key)
    {
        key = null;
        if (request.Mapping is null
            || request.Checkout is null
            || request.CheckoutOwnership is null
            || request.CacheKey is null)
        {
            return false;
        }

        if (!ExternalSourceRepositoryCacheKey.TryCreate(
                request.Mapping.Url,
                request.SolutionPath,
                out key))
        {
            return false;
        }

        return string.Equals(key!.StableValue, request.CacheKey.StableValue, StringComparison.Ordinal)
            && string.Equals(request.LoadedRevision, request.Checkout.LoadedRevision, StringComparison.Ordinal)
            && ExternalSourceRepositoryCacheKey.IsSafeRevision(request.LoadedRevision)
            && (request.ExpectedCurrentGeneration is null
                || ExternalSourceRepositoryCacheContract.IsSafeGenerationName(
                    request.ExpectedCurrentGeneration));
    }

    private static async Task VerifyCheckoutAttestationAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken)
    {
        var verification = await ExternalSourceCheckoutAttestation.VerifyCheckoutAsync(
                request.Checkout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!verification.IsVerified)
        {
            throw new ExternalSourceRepositoryCacheUnsafeSourceException(verification.Trust);
        }
    }

    private static ExternalSourceRepositoryCachePublishResult CreateFailure(
        ExternalSourceRepositoryCachePublishFailureKind failureKind,
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified) =>
        ExternalSourceRepositoryCachePublishResult.Failure(failureKind, [], checkoutTrust);

    private static Task InvokeTestHookAsync(Func<Task>? hook) =>
        hook?.Invoke() ?? Task.CompletedTask;

    private sealed class PublishContext
    {
        internal ExternalSourceRepositoryCachePublishRequest Request { get; init; } = null!;
        internal ExternalSourceRepositoryCacheKey Key { get; init; } = null!;
        internal string EntryDirectory { get; init; } = string.Empty;
        internal string GenerationName { get; init; } = string.Empty;
        internal string GenerationDirectory { get; init; } = string.Empty;
        internal string? PreviousGeneration { get; set; }
        internal bool PointerPublished { get; set; }
    }

}

internal sealed class ExternalSourceRepositoryCachePublishTestSeam
{
    internal Func<Task>? BeforePointerPublishedAsync { get; init; }

    internal Func<Task>? AfterPointerPublishedAsync { get; init; }

    internal Func<Task>? AfterLeaseReleasedAsync { get; init; }
}
