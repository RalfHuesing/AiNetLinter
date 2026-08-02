#nullable enable

using System;
using System.IO;
using System.Linq;

namespace AiNetLinter.Tests.Fixtures;

public abstract class FixtureWorkspaceBase : IDisposable
{
    private readonly TestTempDirectory _tempDir;

    protected FixtureWorkspaceBase(string fixtureFolderName, string tempPrefix)
    {
        _tempDir = TestTempDirectory.Create(tempPrefix);
        RootPath = _tempDir.DirectoryPath;
        var sourceRoot = Path.Combine(FindSolutionRoot(), "tests", "Fixtures", fixtureFolderName);
        CopyFixture(sourceRoot, RootPath);
    }

    public string RootPath { get; }

    public virtual void Dispose()
    {
        _tempDir.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static void CopyFixture(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            if (IsGeneratedPath(relativePath))
            {
                continue;
            }

            var targetFile = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    protected static bool IsGeneratedPath(string relativePath)
    {
        var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains("obj", StringComparer.OrdinalIgnoreCase) ||
               parts.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }

    protected static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }
}
