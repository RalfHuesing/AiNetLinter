#nullable enable

using System;
using System.IO;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

// @covers AssemblyCacheCleanup
[Trait("Category", "Component")]
public sealed class AssemblyCacheCleanupTests
{
    [Fact]
    public void DeleteFile_RemovesExistingFile()
    {
        using var tempDir = TestTempDirectory.Create("assembly-cache-cleanup-file-");
        var path = Path.Combine(tempDir.DirectoryPath, "temporary-pointer.tmp");
        File.WriteAllText(path, "pointer");

        AssemblyCacheCleanup.DeleteFile(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteDirectory_RemovesExistingGeneration()
    {
        using var tempDir = TestTempDirectory.Create("assembly-cache-cleanup-directory-");
        var directory = Path.Combine(tempDir.DirectoryPath, "generation");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), "{}");

        AssemblyCacheCleanup.DeleteDirectory(directory);

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void DeleteFile_IgnoresLockedFileAsBestEffortCleanup()
    {
        using var tempDir = TestTempDirectory.Create("assembly-cache-cleanup-locked-");
        var path = Path.Combine(tempDir.DirectoryPath, "locked-generation.tmp");
        File.WriteAllText(path, "generation");
        using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var exception = Record.Exception(() => AssemblyCacheCleanup.DeleteFile(path));

        Assert.Null(exception);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteDirectory_IgnoresLockedFileInDirectoryAsBestEffortCleanup()
    {
        using var tempDir = TestTempDirectory.Create("assembly-cache-cleanup-locked-dir-");
        var dir = Path.Combine(tempDir.DirectoryPath, "locked-generation");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "locked-file.cs");
        File.WriteAllText(path, "content");
        using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var exception = Record.Exception(() => AssemblyCacheCleanup.DeleteDirectory(dir));

        Assert.Null(exception);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void RetainGenerations_KeepsCurrentAndOnePreviousSafeGeneration()
    {
        using var tempDir = TestTempDirectory.Create("assembly-cache-retention-");
        var names = new[]
        {
            CreateGenerationName(),
            CreateGenerationName(),
            CreateGenerationName(),
        };
        foreach (var name in names)
        {
            Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, name));
        }

        Directory.SetLastWriteTimeUtc(Path.Combine(tempDir.DirectoryPath, names[0]), DateTime.UtcNow.AddMinutes(-3));
        Directory.SetLastWriteTimeUtc(Path.Combine(tempDir.DirectoryPath, names[1]), DateTime.UtcNow.AddMinutes(-2));
        Directory.SetLastWriteTimeUtc(Path.Combine(tempDir.DirectoryPath, names[2]), DateTime.UtcNow.AddMinutes(-1));

        AssemblyCacheCleanup.RetainGenerations(tempDir.DirectoryPath, names[0]);

        Assert.True(Directory.Exists(Path.Combine(tempDir.DirectoryPath, names[0])));
        Assert.True(Directory.Exists(Path.Combine(tempDir.DirectoryPath, names[2])));
        Assert.False(Directory.Exists(Path.Combine(tempDir.DirectoryPath, names[1])));
    }

    private static string CreateGenerationName() =>
        AssemblyCacheContract.GenerationDirectoryPrefix + Guid.NewGuid().ToString("N");
}
