#nullable enable

using System;
using System.IO;
using AiNetLinter.Mcp.Tools.FileStructure;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Unit")]
public sealed class FileTreePathResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRoot_DefaultRoot_ReturnsProjectRoot(string? relativeRoot)
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-default-");
        var projectRoot = tempDir.CreateSubdirectory("repo");

        var result = FileTreePathResolver.ResolveRoot(projectRoot, relativeRoot);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.GetFullPath(projectRoot), result.EffectiveRoot);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("src/tools")]
    [InlineData("src\\tools")]
    public void ResolveRoot_NestedRelativeRoot_ReturnsCanonicalAbsolutePath(string relativeRoot)
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-nested-");
        var projectRoot = tempDir.CreateSubdirectory("repo");
        var expected = Path.GetFullPath(Path.Combine(projectRoot, "src", "tools"));

        var result = FileTreePathResolver.ResolveRoot(projectRoot, relativeRoot);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.EffectiveRoot);
    }

    [Fact]
    public void ResolveRoot_AbsoluteRoot_ReturnsInvalidArgumentWithoutThrowing()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-absolute-");
        var projectRoot = tempDir.CreateSubdirectory("repo");
        var absoluteRoot = Path.Combine(projectRoot, "other");

        var result = FileTreePathResolver.ResolveRoot(projectRoot, absoluteRoot);

        AssertInvalidArgument(result);
        Assert.Contains("relativ", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("..\\outside")]
    [InlineData("nested\\..\\..\\outside")]
    public void ResolveRoot_PathOutsideProjectRoot_ReturnsInvalidArgument(string relativeRoot)
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-outside-");
        var projectRoot = tempDir.CreateSubdirectory("repo");

        var result = FileTreePathResolver.ResolveRoot(projectRoot, relativeRoot);

        AssertInvalidArgument(result);
        Assert.Contains("projectRoot", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRoot_RootPrefixSibling_IsOutsideProjectRoot()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-sibling-");
        var projectRoot = tempDir.CreateSubdirectory("repo");
        var siblingRoot = projectRoot + "-sibling";
        var relativeRoot = Path.GetRelativePath(projectRoot, siblingRoot);

        var result = FileTreePathResolver.ResolveRoot(projectRoot, relativeRoot);

        AssertInvalidArgument(result);
        Assert.Contains("projectRoot", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRoot_InvalidPath_ReturnsInvalidArgumentWithoutThrowing()
    {
        using var tempDir = TestTempDirectory.Create("file-tree-resolver-invalid-");
        var projectRoot = tempDir.CreateSubdirectory("repo");

        var result = FileTreePathResolver.ResolveRoot(projectRoot, "invalid\0");

        AssertInvalidArgument(result);
    }

    private static void AssertInvalidArgument(FileTreePathResolution result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.EffectiveRoot);
        Assert.Equal("INVALID_ARGUMENT", result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
