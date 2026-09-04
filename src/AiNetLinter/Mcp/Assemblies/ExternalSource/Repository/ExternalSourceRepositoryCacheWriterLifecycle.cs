#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Locking;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed partial class LocalExternalSourceRepositoryCacheWriter
{
    private static async Task FinalizePublishAsync(
        PublishContext context,
        bool published,
        ExternalSourceRepositoryCacheKeyLockLease? lockLease,
        AssemblyArtifactFileLockLease? processLock,
        Func<Task>? afterLeaseReleasedAsync)
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
            else
            {
                ExternalSourceRepositoryCacheRetention.RetainGenerations(
                    context.EntryDirectory,
                    context.GenerationName);
            }
        }
        finally
        {
            lockLease?.Dispose();
            processLock?.Dispose();
            await InvokeTestHookAsync(afterLeaseReleasedAsync).ConfigureAwait(false);
        }
    }
}

internal static class ExternalSourceRepositoryCachePublishLifecycle
{
    internal static async Task FinalizeAndReleaseSourceAsync(
        Func<Task> finalizeAsync,
        ExternalSourceCheckoutMaterializationUse? materializationUse)
    {
        try
        {
            await finalizeAsync().ConfigureAwait(false);
        }
        finally
        {
            materializationUse?.Dispose();
        }
    }
}

internal sealed class ExternalSourceRepositoryCacheKeyLockEntry
{
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);

    internal int ReferenceCount { get; set; }
}

internal sealed class ExternalSourceRepositoryCacheKeyLockLease : IDisposable
{
    private readonly ExternalSourceRepositoryCacheKeyLockRegistry registry;
    private readonly string key;
    private readonly ExternalSourceRepositoryCacheKeyLockEntry entry;
    private int disposed;

    internal ExternalSourceRepositoryCacheKeyLockLease(
        ExternalSourceRepositoryCacheKeyLockRegistry registry,
        string key,
        ExternalSourceRepositoryCacheKeyLockEntry entry)
    {
        this.registry = registry;
        this.key = key;
        this.entry = entry;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        registry.Release(key, entry);
    }
}

internal sealed class ExternalSourceRepositoryCacheKeyLockRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, ExternalSourceRepositoryCacheKeyLockEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    internal int Count
    {
        get
        {
            lock (gate) return entries.Count;
        }
    }

    internal async Task<ExternalSourceRepositoryCacheKeyLockLease> AcquireAsync(
        string key,
        CancellationToken cancellationToken)
    {
        ExternalSourceRepositoryCacheKeyLockEntry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(key, out entry!))
            {
                entry = new ExternalSourceRepositoryCacheKeyLockEntry();
                entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ExternalSourceRepositoryCacheKeyLockLease(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    internal void Release(
        string key,
        ExternalSourceRepositoryCacheKeyLockEntry entry)
    {
        lock (gate)
        {
            entry.Semaphore.Release();
            ReleaseReference(key, entry);
        }
    }

    private void ReleaseReference(
        string key,
        ExternalSourceRepositoryCacheKeyLockEntry entry)
    {
        lock (gate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0
                && entries.TryGetValue(key, out var current)
                && ReferenceEquals(current, entry))
            {
                entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }
}

internal static class ExternalSourceRepositoryCacheRetention
{
    internal static void RetainGenerations(string entryDirectory, string currentGeneration)
    {
        try
        {
            if (!Directory.Exists(entryDirectory)
                || ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(entryDirectory))
            {
                return;
            }

            var generations = Directory.EnumerateDirectories(
                    entryDirectory,
                    ExternalSourceRepositoryCacheContract.GenerationDirectoryPrefix + "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path => ExternalSourceRepositoryCacheContract.IsSafeGenerationName(Path.GetFileName(path)))
                .Where(path => !ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(path))
                .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                .ThenByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToList();
            var retained = new HashSet<string>(StringComparer.Ordinal)
            {
                currentGeneration,
            };
            foreach (var generation in generations)
            {
                var name = Path.GetFileName(generation);
                if (retained.Contains(name))
                {
                    continue;
                }

                if (retained.Count < ExternalSourceRepositoryCacheContract.MaxRetainedGenerations)
                {
                    retained.Add(name);
                    continue;
                }

                ExternalSourceRepositoryCacheStorage.TryDeleteGeneration(entryDirectory, generation);
            }
        }
        catch (Exception ignored) when (ExternalSourceRepositoryCacheStorage.IsCacheException(ignored))
        {
        }
    }
}

internal sealed class ExternalSourceRepositoryCacheGenerationLease : IDisposable
{
    private readonly FileStream generationStream;
    private int disposed;

    private ExternalSourceRepositoryCacheGenerationLease(FileStream generationStream)
    {
        this.generationStream = generationStream;
    }

    internal static bool TryAcquireReader(
        string entryDirectory,
        string generationName,
        out ExternalSourceRepositoryCacheGenerationLease? lease)
    {
        lease = null;
        if (!TryOpenProcessReadLock(entryDirectory, out var processReadStream)
            || processReadStream is null)
        {
            return false;
        }

        try
        {
            if (!TryOpenGeneration(
                    entryDirectory,
                    generationName,
                    FileAccess.Read,
                    FileShare.Read,
                    out var generationStream)
                || generationStream is null)
            {
                return false;
            }

            lease = new ExternalSourceRepositoryCacheGenerationLease(generationStream);
            return true;
        }
        finally
        {
            processReadStream.Dispose();
        }
    }

    internal static bool TryAcquireDeletion(
        string entryDirectory,
        string generationName,
        out ExternalSourceRepositoryCacheGenerationLease? lease)
    {
        lease = null;
        if (!TryOpenGeneration(
                entryDirectory,
                generationName,
                FileAccess.ReadWrite,
                FileShare.None,
                out var generationStream)
            || generationStream is null)
        {
            return false;
        }

        lease = new ExternalSourceRepositoryCacheGenerationLease(generationStream);
        return true;
    }

    internal static string GetLockPath(string entryDirectory, string generationName) =>
        Path.Combine(entryDirectory, generationName + ".reader.lock");

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        generationStream.Dispose();
    }

    private static bool TryOpenProcessReadLock(
        string entryDirectory,
        out FileStream? stream)
    {
        stream = null;
        if (string.IsNullOrWhiteSpace(entryDirectory)
            || !Directory.Exists(entryDirectory)
            || ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(entryDirectory))
        {
            return false;
        }

        var lockPath = Path.Combine(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.ProcessLockFileName);
        if (Directory.Exists(lockPath)
            || File.Exists(lockPath)
                && ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(lockPath))
        {
            return false;
        }

        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.SequentialScan);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryOpenGeneration(
        string entryDirectory,
        string generationName,
        FileAccess access,
        FileShare share,
        out FileStream? stream)
    {
        stream = null;
        if (string.IsNullOrWhiteSpace(entryDirectory)
            || !Directory.Exists(entryDirectory)
            || !ExternalSourceRepositoryCacheContract.IsSafeGenerationName(generationName)
            || ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(entryDirectory))
        {
            return false;
        }

        var lockPath = GetLockPath(entryDirectory, generationName);
        if (Directory.Exists(lockPath)
            || File.Exists(lockPath)
                && ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(lockPath))
        {
            return false;
        }

        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                access,
                share,
                bufferSize: 1,
                FileOptions.SequentialScan);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
