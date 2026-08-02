#nullable enable

using System;
using System.IO;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Disposable wrapper for a temporary test directory created via <see cref="Directory.CreateTempSubdirectory"/>.
/// Safely deletes directory contents on dispose.
/// </summary>
public sealed class TestTempDirectory : IDisposable
{
    public string DirectoryPath { get; }

    private TestTempDirectory(string prefix)
    {
        DirectoryPath = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    public static TestTempDirectory Create(string prefix = "AiNetTest_") => new(prefix);

    public string CreateSubdirectory(string relativePath)
    {
        var fullPath = Path.Combine(DirectoryPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string CreateFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(DirectoryPath, relativePath);
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            Directory.CreateDirectory(parent);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch
        {
            // Ignore temporary directory cleanup failures during test cleanup
        }
    }

    public static implicit operator string(TestTempDirectory tempDir) => tempDir.DirectoryPath;
}
