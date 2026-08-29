#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal interface IExternalSourceRepositoryCacheWriter
{
    Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class LocalExternalSourceRepositoryCacheWriter : IExternalSourceRepositoryCacheWriter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string cacheRoot;
    private readonly Action? afterPointerPublished;

    internal LocalExternalSourceRepositoryCacheWriter(
        string? cacheRoot = null,
        Action? afterPointerPublished = null)
    {
        var configuredRoot = cacheRoot
            ?? System.IO.Path.Combine(AppContext.BaseDirectory, "cache", "source");
        this.cacheRoot = ExternalSourceRepositoryCacheContract.TryCanonicalizeAbsoluteRoot(configuredRoot)
            ?? throw new ArgumentException(
                "Die Cache-Wurzel muss ein absoluter, gültiger Pfad sein.",
                nameof(cacheRoot));
        this.afterPointerPublished = afterPointerPublished;
    }

    internal string GetEntryDirectory(ExternalSourceRepositoryCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return System.IO.Path.Combine(cacheRoot, key.StableValue);
    }

    public async Task<ExternalSourceRepositoryCachePublishResult> PublishAsync(
        ExternalSourceRepositoryCachePublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryValidateRequest(request, out var key, out var failure))
        {
            return failure!;
        }

        var entryDirectory = GetEntryDirectory(key!);
        var generationName = ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix
            + Guid.NewGuid().ToString("N");
        var generationDirectory = System.IO.Path.Combine(entryDirectory, generationName);
        var context = new PublishContext
        {
            Request = request,
            Key = key!,
            EntryDirectory = entryDirectory,
            GenerationName = generationName,
            GenerationDirectory = generationDirectory,
        };
        var published = false;
        CacheKeyLockLease? lockLease = null;
        try
        {
            lockLease = await AcquireLockAsync(entryDirectory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result = PublishGeneration(context, cancellationToken);
            published = result.Succeeded;
            return result;
        }
        catch (OperationCanceledException)
        {
            return CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.Cancelled);
        }
        catch (ExternalSourceRepositoryCacheUnsafeSourceException)
        {
            return CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.UnsafeSource);
        }
        catch (Exception exception) when (ExternalSourceRepositoryCacheStorage.IsCacheException(exception))
        {
            return CreateFailure(ExternalSourceRepositoryCachePublishFailureKind.WriteFailed);
        }
        finally
        {
            FinalizePublish(context, published, lockLease);
        }
    }

    private static void FinalizePublish(
        PublishContext context,
        bool published,
        CacheKeyLockLease? lockLease)
    {
        try
        {
            if (!published)
            {
                if (context.PointerPublished)
                {
                    ExternalSourceRepositoryCacheStorage.RestorePreviousCurrent(
                        context.EntryDirectory,
                        context.GenerationName,
                        context.PreviousGeneration);
                }

                ExternalSourceRepositoryCacheStorage.TryDeleteGeneration(
                    context.EntryDirectory,
                    context.GenerationDirectory);
            }
        }
        finally
        {
            lockLease?.Dispose();
        }
    }

    private ExternalSourceRepositoryCachePublishResult PublishGeneration(
        PublishContext context,
        CancellationToken cancellationToken)
    {
        ExternalSourceRepositoryCacheStorage.PrepareDirectory(cacheRoot);
        ExternalSourceRepositoryCacheStorage.PrepareEntryDirectory(
            cacheRoot,
            context.EntryDirectory);
        ExternalSourceRepositoryCacheStorage.ValidateSourceCheckout(
            context.Request,
            context.Key);
        var previous = TryReadCurrent(
            new ExternalSourceRepositoryCacheReadRequest
            {
                Key = context.Key,
                EntryDirectory = context.EntryDirectory,
            },
            out var previousCurrent,
            out _)
            ? previousCurrent!.Manifest.GenerationName
            : null;
        context.PreviousGeneration = previous;

        System.IO.Directory.CreateDirectory(context.GenerationDirectory);
        WriteGeneration(
            context.Request,
            context.Key,
            context.GenerationDirectory,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ExternalSourceRepositoryCacheStorage.TryPublishPointer(
                context.EntryDirectory,
                context.GenerationName,
                previous,
                out var pointerFailure))
        {
            return pointerFailure!;
        }

        context.PointerPublished = true;
        afterPointerPublished?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
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

    private static async Task<CacheKeyLockLease> AcquireLockAsync(
        string entryDirectory,
        CancellationToken cancellationToken)
    {
        var lockKey = System.IO.Path.GetFullPath(entryDirectory);
        var gate = Locks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new CacheKeyLockLease(gate);
    }

    private static bool TryValidateRequest(
        ExternalSourceRepositoryCachePublishRequest request,
        out ExternalSourceRepositoryCacheKey? key,
        out ExternalSourceRepositoryCachePublishResult? failure)
    {
        key = null;
        failure = null;
        if (request.Mapping is null
            || request.Checkout is null
            || request.CheckoutOwnership is null
            || request.CacheKey is null
            || !ExternalSourceRepositoryCacheKey.TryCreate(
                request.Mapping.Url,
                request.SolutionPath,
                out key)
            || !string.Equals(key!.StableValue, request.CacheKey.StableValue, StringComparison.Ordinal)
            || !string.Equals(request.LoadedRevision, request.Checkout.LoadedRevision, StringComparison.Ordinal)
            || !ExternalSourceRepositoryCacheKey.IsSafeRevision(request.LoadedRevision))
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

        return true;
    }

    private static ExternalSourceRepositoryCachePublishResult CreateFailure(
        ExternalSourceRepositoryCachePublishFailureKind failureKind) =>
        ExternalSourceRepositoryCachePublishResult.Failure(failureKind);

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

    private sealed class CacheKeyLockLease : IDisposable
    {
        private readonly SemaphoreSlim gate;
        private int disposed;

        internal CacheKeyLockLease(SemaphoreSlim gate)
        {
            this.gate = gate;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            gate.Release();
        }
    }
}
