#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace AiNetLinter.Cache;

internal sealed class AnalysisCacheManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _cachePath;
    private readonly AnalysisCacheFile _cache;
    private readonly Lock _lock = new();
    private bool _dirty;

    private AnalysisCacheManager(string cachePath, AnalysisCacheFile cache)
    {
        _cachePath = cachePath;
        _cache = cache;
    }

    public static AnalysisCacheManager Load(string exeDir, string solutionPath, string rulesJsonContent, TimeSpan cacheTtl)
    {
        var cacheDir = Path.Combine(exeDir, "cache");
        Directory.CreateDirectory(cacheDir);

        PurgeStale(cacheDir, cacheTtl);

        var prefix = BuildCacheFilePrefix(solutionPath, rulesJsonContent);
        var fileName = $"{prefix}-{GetBuildTimestamp()}.json";
        var cachePath = Path.Combine(cacheDir, fileName);

        CleanupOldCacheFiles(cacheDir, prefix, fileName);

        var cache = TryReadCache(cachePath) ?? new AnalysisCacheFile();
        return new AnalysisCacheManager(cachePath, cache);
    }

    private static void PurgeStale(string cacheDir, TimeSpan ttl)
    {
        if (ttl == TimeSpan.Zero) return;

        var cutoff = DateTime.UtcNow - ttl;
        foreach (var file in Directory.EnumerateFiles(cacheDir, "*.json"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch (Exception ignored) { _ = ignored; }
        }
    }

    public bool TryGet(string relativePath, string currentChecksum, out AnalysisCacheEntry? entry)
    {
        lock (_lock)
        {
            entry = null;
            if (!_cache.Files.TryGetValue(relativePath, out var cached)) return false;
            if (cached.Checksum != currentChecksum) return false;
            entry = cached;
            return true;
        }
    }

    public void Set(string relativePath, AnalysisCacheEntry entry)
    {
        lock (_lock)
        {
            _cache.Files[relativePath] = entry;
            _dirty = true;
        }
    }

    public void SaveIfDirty()
    {
        if (!_dirty) return;
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        File.WriteAllText(_cachePath, json, Encoding.UTF8);
        _dirty = false;
    }

    private static string BuildCacheFilePrefix(string solutionPath, string rulesJsonContent)
    {
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var hashInput = solutionPath.ToLowerInvariant() + rulesJsonContent;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hash8 = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return $"{solutionName}-{hash8}";
    }

    private static string GetBuildTimestamp()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            return "unknown";
        return File.GetLastWriteTimeUtc(assemblyPath).ToString("yyyyMMddHHmmss");
    }

    private static void CleanupOldCacheFiles(string cacheDir, string prefix, string currentFileName)
    {
        foreach (var file in Directory.EnumerateFiles(cacheDir, $"{prefix}-*.json"))
        {
            if (Path.GetFileName(file).Equals(currentFileName, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(file); }
            catch (Exception ignored) { _ = ignored; }
        }
    }

    private static AnalysisCacheFile? TryReadCache(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var file = JsonSerializer.Deserialize<AnalysisCacheFile>(json);
            if (file?.SchemaVersion != AnalysisCacheFile.CurrentSchemaVersion) return null;
            // System.Text.Json erstellt ein neues Dictionary mit case-sensitivem Comparer.
            // Den OrdinalIgnoreCase-Comparer explizit wiederherstellen.
            return file with
            {
                Files = new Dictionary<string, AnalysisCacheEntry>(
                    file.Files, StringComparer.OrdinalIgnoreCase)
            };
        }
        catch (Exception ignored)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read cache file: {ignored.Message}");
            return null;
        }
    }
}
