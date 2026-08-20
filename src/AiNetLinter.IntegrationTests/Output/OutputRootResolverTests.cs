#nullable enable

using System;
using System.IO;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.IntegrationTests.Output;

[Trait("Category", "Integration")]
public sealed class OutputRootResolverTests
{
    [Fact]
    public void Resolve_ReturnsFullPathForDirectory()
    {
        using var tempDir = TestTempDirectory.Create("output-res-");

        var result = OutputRootResolver.Resolve(tempDir.DirectoryPath);

        Assert.Equal(Path.GetFullPath(tempDir.DirectoryPath), result);
    }

    [Fact]
    public void Resolve_ReturnsParentDirectoryForSolutionFile()
    {
        using var tempDir = TestTempDirectory.Create("output-res-");
        var slnxPath = tempDir.CreateFile("App.slnx", "<Solution />");

        var result = OutputRootResolver.Resolve(slnxPath);

        Assert.Equal(Path.GetFullPath(tempDir.DirectoryPath), result);
    }

    [Fact]
    public void Resolve_ThrowsWhenPathDoesNotExist()
    {
        using var tempDir = TestTempDirectory.Create("output-res-");
        var missing = tempDir.GetPath("nonexistent");

        Assert.Throws<DirectoryNotFoundException>(() => OutputRootResolver.Resolve(missing));
    }
}
