#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyCacheKeyLockRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, AssemblyCacheKeyLockEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    internal AssemblyCacheKeyLockLease Acquire(string entryDirectory)
    {
        var key = Path.GetFullPath(entryDirectory);
        AssemblyCacheKeyLockEntry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(key, out entry!))
            {
                entry = new AssemblyCacheKeyLockEntry();
                entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            System.Threading.Monitor.Enter(entry.Gate);
            return new(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    internal void Release(string key, AssemblyCacheKeyLockEntry entry)
    {
        System.Threading.Monitor.Exit(entry.Gate);
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, AssemblyCacheKeyLockEntry entry)
    {
        lock (gate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0
                && entries.TryGetValue(key, out var current)
                && ReferenceEquals(current, entry))
            {
                entries.Remove(key);
            }
        }
    }
}

internal sealed class AssemblyCacheKeyLockEntry
{
    internal object Gate { get; } = new();
    internal int ReferenceCount { get; set; }
}

internal sealed class AssemblyCacheKeyLockLease : IDisposable
{
    private readonly AssemblyCacheKeyLockRegistry registry;
    private readonly string key;
    private readonly AssemblyCacheKeyLockEntry entry;
    private int disposed;

    internal AssemblyCacheKeyLockLease(
        AssemblyCacheKeyLockRegistry registry,
        string key,
        AssemblyCacheKeyLockEntry entry)
    {
        this.registry = registry;
        this.key = key;
        this.entry = entry;
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0) return;
        registry.Release(key, entry);
    }
}
