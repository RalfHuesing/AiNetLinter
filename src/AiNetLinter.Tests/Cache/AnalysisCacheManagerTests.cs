#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AiNetLinter.Cache;

namespace AiNetLinter.Tests.Cache;

public sealed class AnalysisCacheManagerTests : IDisposable
{
    private readonly string _tempDir;

    public AnalysisCacheManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-cachetests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch (Exception ignored)
        {
            System.Diagnostics.Debug.WriteLine($"Cleanup failed: {ignored.Message}");
        }
    }

    [Fact]
    public void CacheManager_LoadAndSave_WorksCorrectly()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        Assert.NotNull(manager);

        var relativePath = "src/Test.cs";
        var checksum = "abcde12345";
        var entry = new AnalysisCacheEntry
        {
            RelativePath = relativePath,
            Checksum = checksum,
            Violations = new[]
            {
                new RuleViolationDto("src/Test.cs", 10, "RuleName", "Details", "Guidance")
            }
        };

        manager.Set(relativePath, entry);
        manager.SaveIfDirty();

        // Load again
        var manager2 = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        var found = manager2.TryGet(relativePath, checksum, out var loadedEntry);
        Assert.True(found);
        Assert.NotNull(loadedEntry);
        Assert.Equal(relativePath, loadedEntry.RelativePath);
        Assert.Equal(checksum, loadedEntry.Checksum);
        Assert.Single(loadedEntry.Violations);
        Assert.Equal("RuleName", loadedEntry.Violations[0].RuleName);
    }

    [Fact]
    public void CacheManager_SchemaMismatch_InvalidatesCache()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        var relativePath = "src/Test.cs";
        var checksum = "abcde12345";
        var entry = new AnalysisCacheEntry
        {
            RelativePath = relativePath,
            Checksum = checksum
        };
        manager.Set(relativePath, entry);
        manager.SaveIfDirty();

        // Modify schema version in the file directly to 999
        var cacheDir = Path.Combine(_tempDir, "cache");
        var cacheFiles = Directory.GetFiles(cacheDir, "*.json");
        Assert.Single(cacheFiles);
        var cacheFilePath = cacheFiles[0];

        var content = File.ReadAllText(cacheFilePath);
        var modifiedContent = content.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":999");
        File.WriteAllText(cacheFilePath, modifiedContent);

        // Load again with mismatched schema version -> should return new (empty) cache
        var manager2 = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        var found = manager2.TryGet(relativePath, checksum, out var loadedEntry);
        Assert.False(found);
        Assert.Null(loadedEntry);
    }

    [Fact]
    public void CacheManager_TryGetWithMismatchedChecksum_ReturnsFalse()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        var relativePath = "src/Test.cs";
        var checksum = "abcde12345";
        var entry = new AnalysisCacheEntry
        {
            RelativePath = relativePath,
            Checksum = checksum
        };
        manager.Set(relativePath, entry);

        var found = manager.TryGet(relativePath, "different_checksum", out var loadedEntry);
        Assert.False(found);
        Assert.Null(loadedEntry);
    }

    [Fact]
    public void CacheManager_ConcurrentGetAndSet_DoesNotThrow()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";
        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
        {
            try
            {
                var path = $"src/File{i % 10}.cs";
                var entry = new AnalysisCacheEntry { RelativePath = path, Checksum = $"hash{i}" };
                manager.Set(path, entry);
                manager.TryGet(path, $"hash{i}", out _);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        Task.WaitAll([.. tasks]);
        Assert.Empty(exceptions);
    }

    [Fact]
    public void CacheManager_OrdinalIgnoreCasePreservedAfterRoundtrip()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        var relativePath = "src/Auth/JwtService.cs";
        var checksum = "abc123";
        manager.Set(relativePath, new AnalysisCacheEntry { RelativePath = relativePath, Checksum = checksum });
        manager.SaveIfDirty();

        // Nach Deserialisierung muss der Lookup case-insensitiv funktionieren
        var manager2 = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent, TimeSpan.FromMinutes(60));
        Assert.True(manager2.TryGet("SRC/AUTH/JWTSERVICE.CS", checksum, out var entry));
        Assert.NotNull(entry);
    }

    [Fact]
    public void PurgeStale_DeletesFilesOlderThanTtl()
    {
        var cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(cacheDir);

        var oldFile = Path.Combine(cacheDir, "old-cache.json");
        File.WriteAllText(oldFile, "{}");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddMinutes(-90));

        var freshFile = Path.Combine(cacheDir, "fresh-cache.json");
        File.WriteAllText(freshFile, "{}");

        AnalysisCacheManager.Load(_tempDir, "Dummy.sln", "{}", TimeSpan.FromMinutes(60));

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(freshFile));
    }

    [Fact]
    public void PurgeStale_WithZeroTtl_DeletesNothing()
    {
        var cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(cacheDir);

        var oldFile = Path.Combine(cacheDir, "old.json");
        File.WriteAllText(oldFile, "{}");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-7));

        AnalysisCacheManager.Load(_tempDir, "Dummy.sln", "{}", TimeSpan.Zero);

        Assert.True(File.Exists(oldFile));
    }
}
