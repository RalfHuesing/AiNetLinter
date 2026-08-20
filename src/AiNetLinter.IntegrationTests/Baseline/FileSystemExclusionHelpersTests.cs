#nullable enable

using System;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class FileSystemExclusionHelpersTests
{
    [Fact]
    public void IsGeneratedPath_ObjSubdir_ReturnsTrue()
    {
        var path = Path.Combine("repo", "src", "obj", "Debug", "Foo.cs");

        Assert.True(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void IsGeneratedPath_BinSubdir_ReturnsTrue()
    {
        var path = Path.Combine("repo", "src", "bin", "Release", "Foo.cs");

        Assert.True(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void IsGeneratedPath_NodeModulesSubdir_ReturnsTrue()
    {
        var path = Path.Combine("repo", "wwwroot", "node_modules", "react", "index.js");

        Assert.True(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void IsGeneratedPath_NormalPath_ReturnsFalse()
    {
        var path = Path.Combine("repo", "src", "Project", "Foo.cs");

        Assert.False(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void IsGeneratedPath_ClaudeWorktreesSubdir_ReturnsTrue()
    {
        var path = Path.Combine("repo", ".claude", "worktrees", "agent-x", "src", "Foo.cs");

        Assert.True(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void IsGeneratedPath_DotWorktreesSubdir_ReturnsTrue()
    {
        var path = Path.Combine("repo", ".worktrees", "agent-branch", "src", "Foo.cs");

        Assert.True(FileSystemExclusionHelpers.IsGeneratedPath(path));
    }

    [Fact]
    public void SafeEnumerateFiles_NonExistentDir_ReturnsEmpty()
    {
        var nonExistent = Path.Combine(TestTempDirectory.RootTempDirectory, Guid.NewGuid().ToString("N"));

        var result = FileSystemExclusionHelpers.SafeEnumerateFiles(nonExistent).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void SafeEnumerateFiles_ExistingDir_ReturnsAllFilesIncludingGenerated()
    {
        using var temp = TestTempDirectory.Create("ainetlinter-fseh-");
        var keep = Path.Combine(temp.DirectoryPath, "src", "Project", "Keep.cs");
        var generated = Path.Combine(temp.DirectoryPath, "src", "Project", "obj", "Gen.cs");
        var generatedBin = Path.Combine(temp.DirectoryPath, "src", "Project", "bin", "Gen.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(keep)!);
        Directory.CreateDirectory(Path.GetDirectoryName(generated)!);
        Directory.CreateDirectory(Path.GetDirectoryName(generatedBin)!);
        File.WriteAllText(keep, "// keep");
        File.WriteAllText(generated, "// gen");
        File.WriteAllText(generatedBin, "// gen");

        var enumerated = FileSystemExclusionHelpers
            .SafeEnumerateFiles(temp.DirectoryPath)
            .ToList();
        var filtered = enumerated
            .Where(p => !FileSystemExclusionHelpers.IsGeneratedPath(p))
            .ToList();

        Assert.Contains(keep, enumerated);
        Assert.Contains(generated, enumerated);
        Assert.Contains(generatedBin, enumerated);
        Assert.Contains(keep, filtered);
        Assert.DoesNotContain(generated, filtered);
        Assert.DoesNotContain(generatedBin, filtered);
    }
}
