#nullable enable

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
}
