#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Platform;

[Trait("Category", "Component")]
public sealed class TestTempDirectoryTests
{
    [Fact]
    public void Dispose_DeletesDirectoryAndIsIdempotent()
    {
        var temp = TestTempDirectory.Create("test-temp-dispose-");
        try
        {
            var path = temp.DirectoryPath;
            temp.CreateFile("nested", "content");

            temp.Dispose();
            temp.Dispose();

            Assert.False(Directory.Exists(path));
        }
        finally
        {
            temp.Dispose();
        }
    }

    [Fact]
    public void Create_DoesNotDeleteAnotherActiveDirectory()
    {
        using var active = TestTempDirectory.Create("test-temp-active-");
        using var trigger = TestTempDirectory.Create("test-temp-trigger-");

        Assert.True(Directory.Exists(active.DirectoryPath));
    }

    [Fact]
    public void Create_DeletesOldUnownedGuidDirectory()
    {
        Directory.CreateDirectory(TestTempDirectory.RootTempDirectory);
        var stalePath = Path.Combine(
            TestTempDirectory.RootTempDirectory,
            $"stale-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stalePath);
        File.WriteAllText(Path.Combine(stalePath, "stale.txt"), "stale");
        Directory.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddDays(-2));

        try
        {
            using var current = TestTempDirectory.Create("test-temp-stale-");

            Assert.False(Directory.Exists(stalePath));
        }
        finally
        {
            if (Directory.Exists(stalePath))
            {
                Directory.Delete(stalePath, recursive: true);
            }
        }
    }
}
