#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.Locking;

/// <summary>
/// Prozessübergreifender, OS-Level-Exklusivlock pro Artefaktschlüssel (Verzeichnispfad).
/// Intern hält ein aktiver Erzeuger eine Lockdatei mit <see cref="FileShare.None"/> geöffnet.
/// Bei Prozessabbruch gibt das Betriebssystem das Handle automatisch frei.
/// </summary>
internal sealed class AssemblyArtifactFileLockRegistry
{
    private static readonly TimeSpan DefaultStallThreshold = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(50);

    private readonly object gate = new();
    private readonly Dictionary<string, AssemblyArtifactFileLockEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly string lockFileNameSuffix;

    internal AssemblyArtifactFileLockRegistry(string lockFileNameSuffix = "build.lock")
    {
        this.lockFileNameSuffix = lockFileNameSuffix;
    }

    /// <summary>
    /// Akquiriert den exklusiven Erzeuger-Lock für <paramref name="entryDirectory"/>.
    /// Wartet cancellierbar bis <paramref name="stallThreshold"/>;
    /// nach Ablauf wird eine Stall-Lease gemeldet und der Aufrufer kann abbrechen.
    /// </summary>
    internal async Task<AssemblyArtifactFileLockLease> AcquireAsync(
        string entryDirectory,
        CancellationToken cancellationToken,
        TimeSpan? stallThreshold = null)
    {
        var key = Path.GetFullPath(entryDirectory);
        var lockPath = Path.Combine(key, lockFileNameSuffix);
        var threshold = stallThreshold ?? DefaultStallThreshold;
        var startTimestamp = Stopwatch.GetTimestamp();

        var entry = GetOrCreateEntry(key);
        try
        {
            return await WaitForLockAsync(key, lockPath, entry, startTimestamp, threshold, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    private AssemblyArtifactFileLockEntry GetOrCreateEntry(string key)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entry = new AssemblyArtifactFileLockEntry(key);
                entries[key] = entry;
            }

            entry.ReferenceCount++;
            return entry;
        }
    }

    private async Task<AssemblyArtifactFileLockLease> WaitForLockAsync(
        string key,
        string lockPath,
        AssemblyArtifactFileLockEntry entry,
        long startTimestamp,
        TimeSpan threshold,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            if (elapsed >= threshold)
            {
                ReleaseReference(key, entry);
                return AssemblyArtifactFileLockLease.Stalled(key, threshold);
            }

            var remaining = threshold - elapsed;
            var acquired = await entry.TryAcquireInProcessAsync(remaining, cancellationToken).ConfigureAwait(false);
            if (!acquired)
            {
                ReleaseReference(key, entry);
                return AssemblyArtifactFileLockLease.Stalled(key, threshold);
            }

            var stream = TryAcquireLockFile(lockPath, entry);
            if (stream is not null)
            {
                return new AssemblyArtifactFileLockLease(this, key, entry, lockPath, stream);
            }

            var delayTime = remaining < PollingInterval ? remaining : PollingInterval;
            if (delayTime <= TimeSpan.Zero)
            {
                ReleaseReference(key, entry);
                return AssemblyArtifactFileLockLease.Stalled(key, threshold);
            }

            await Task.Delay(delayTime, cancellationToken).ConfigureAwait(false);
        }
    }

    private static FileStream? TryAcquireLockFile(string lockPath, AssemblyArtifactFileLockEntry entry)
    {
        try
        {
            var dir = Path.GetDirectoryName(lockPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var stream = TryOpenLockFile(lockPath);
            if (stream is not null)
            {
                entry.SetHeld(stream);
                return stream;
            }
        }
        catch
        {
            entry.ReleaseInProcess();
            throw;
        }

        entry.ReleaseInProcess();
        return null;
    }

    internal void Release(string key, AssemblyArtifactFileLockEntry entry)
    {
        entry.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, AssemblyArtifactFileLockEntry entry)
    {
        lock (gate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount <= 0 && entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                entries.Remove(key);
            }
        }
    }

    private static FileStream? TryOpenLockFile(string path)
    {
        try
        {
            return new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

internal sealed class AssemblyArtifactFileLockEntry
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private FileStream? lockStream;

    internal string Key { get; }
    internal int ReferenceCount { get; set; }
    internal bool IsHeld => lockStream is not null;

    internal AssemblyArtifactFileLockEntry(string key)
    {
        Key = key;
    }

    internal async Task<bool> TryAcquireInProcessAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    internal void ReleaseInProcess()
    {
        semaphore.Release();
    }

    internal void SetHeld(FileStream stream)
    {
        lockStream = stream;
    }

    internal void Release()
    {
        var stream = Interlocked.Exchange(ref lockStream, null);
        stream?.Dispose();
        semaphore.Release();
    }
}

internal sealed class AssemblyArtifactFileLockLease : IDisposable, IAsyncDisposable
{
    private readonly AssemblyArtifactFileLockRegistry? registry;
    private readonly AssemblyArtifactFileLockEntry? entry;
    private readonly string? lockPath;
    private readonly FileStream? stream;
    private int disposed;

    internal string Key { get; }
    internal bool IsStalled { get; }
    internal TimeSpan? StallThreshold { get; }

    internal AssemblyArtifactFileLockLease(
        AssemblyArtifactFileLockRegistry registry,
        string key,
        AssemblyArtifactFileLockEntry entry,
        string lockPath,
        FileStream stream)
    {
        this.registry = registry;
        Key = key;
        this.entry = entry;
        this.lockPath = lockPath;
        this.stream = stream;
        IsStalled = false;
    }

    private AssemblyArtifactFileLockLease(string key, TimeSpan stallThreshold)
    {
        Key = key;
        IsStalled = true;
        StallThreshold = stallThreshold;
    }

    internal static AssemblyArtifactFileLockLease Stalled(string key, TimeSpan stallThreshold)
    {
        return new AssemblyArtifactFileLockLease(key, stallThreshold);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (registry is not null && entry is not null)
        {
            registry.Release(Key, entry);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
