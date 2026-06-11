using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

public sealed class OutputRootResolverTests
{
    [Fact]
    public void Resolve_ReturnsFullPathForDirectory()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

        try
        {
            var result = OutputRootResolver.Resolve(tempDir);

            Assert.Equal(Path.GetFullPath(tempDir), result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ReturnsParentDirectoryForSolutionFile()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var slnxPath = Path.Combine(tempDir, "App.slnx");
        File.WriteAllText(slnxPath, "<Solution />");

        try
        {
            var result = OutputRootResolver.Resolve(slnxPath);

            Assert.Equal(Path.GetFullPath(tempDir), result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ThrowsWhenPathDoesNotExist()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Assert.Throws<DirectoryNotFoundException>(() => OutputRootResolver.Resolve(missing));
    }
}
