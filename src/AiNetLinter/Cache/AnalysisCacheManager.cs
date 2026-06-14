#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Cache;

internal sealed class AnalysisCacheManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _cachePath;
    private readonly AnalysisCacheFile _cache;
    private bool _dirty;

    private AnalysisCacheManager(string cachePath, AnalysisCacheFile cache)
    {
        _cachePath = cachePath;
        _cache = cache;
    }

    public static AnalysisCacheManager Load(string exeDir, string solutionPath, string rulesJsonContent)
    {
        var cacheDir = Path.Combine(exeDir, "cache");
        Directory.CreateDirectory(cacheDir);

        var fileName = BuildCacheFileName(solutionPath, rulesJsonContent);
        var cachePath = Path.Combine(cacheDir, fileName);

        var cache = TryReadCache(cachePath) ?? new AnalysisCacheFile();
        return new AnalysisCacheManager(cachePath, cache);
    }

    public bool TryGet(string relativePath, string currentChecksum, out AnalysisCacheEntry? entry)
    {
        entry = null;
        if (!_cache.Files.TryGetValue(relativePath, out var cached)) return false;
        if (cached.Checksum != currentChecksum) return false;
        entry = cached;
        return true;
    }

    public void Set(string relativePath, AnalysisCacheEntry entry)
    {
        _cache.Files[relativePath] = entry;
        _dirty = true;
    }

    public void SaveIfDirty()
    {
        if (!_dirty) return;
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        File.WriteAllText(_cachePath, json, Encoding.UTF8);
        _dirty = false;
    }

    private static string BuildCacheFileName(string solutionPath, string rulesJsonContent)
    {
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var hashInput = solutionPath.ToLowerInvariant() + rulesJsonContent;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hash8 = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return $"{solutionName}-{hash8}.json";
    }

    private static AnalysisCacheFile? TryReadCache(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var file = JsonSerializer.Deserialize<AnalysisCacheFile>(json);
            if (file?.SchemaVersion != AnalysisCacheFile.CurrentSchemaVersion) return null;
            return file;
        }
        catch (Exception ignored)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read cache file: {ignored.Message}");
            return null;
        }
    }
}
