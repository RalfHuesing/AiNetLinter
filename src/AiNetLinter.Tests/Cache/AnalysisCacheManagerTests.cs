#nullable enable
using System;
using System.IO;
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

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent);
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
        var manager2 = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent);
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

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent);
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
        var manager2 = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent);
        var found = manager2.TryGet(relativePath, checksum, out var loadedEntry);
        Assert.False(found);
        Assert.Null(loadedEntry);
    }

    [Fact]
    public void CacheManager_TryGetWithMismatchedChecksum_ReturnsFalse()
    {
        var solutionPath = Path.Combine(_tempDir, "TestSolution.sln");
        var rulesContent = "{ \"Global\": {} }";

        var manager = AnalysisCacheManager.Load(_tempDir, solutionPath, rulesContent);
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
}
