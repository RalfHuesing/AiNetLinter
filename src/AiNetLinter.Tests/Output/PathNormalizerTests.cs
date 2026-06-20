using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

public sealed class PathNormalizerTests
{
    [Fact]
    public void ToRelative_ConvertsAbsolutePathToRelativeWithForwardSlashes()
    {
        var root = Path.GetFullPath(@"C:\Projects\MyApp");
        var absolute = Path.Combine(root, "src", "Core", "Foo.cs");

        var result = PathNormalizer.ToRelative(root, absolute);

        Assert.Equal("src/Core/Foo.cs", result);
    }

    [Fact]
    public void ToRelative_ReturnsFileNameWhenOutsideOutputRoot()
    {
        var root = Path.GetFullPath(@"C:\Projects\MyApp");
        var outside = Path.GetFullPath(@"C:\Other\Bar.cs");

        var result = PathNormalizer.ToRelative(root, outside);

        Assert.Equal("Bar.cs", result);
    }

    [Fact]
    public void ToRelative_ReturnsEmptyForNullOrEmptyPath()
    {
        var root = Path.GetFullPath(@"C:\Projects\MyApp");

        Assert.Equal(string.Empty, PathNormalizer.ToRelative(root, ""));
    }

    [Theory]
    [InlineData("src/AiNetLinter.Tests/Foo.cs", true)]
    [InlineData("src\\AiNetLinter.Tests\\Foo.cs", true)]
    [InlineData("src/AiNetLinter.tests/Foo.cs", true)]
    [InlineData("src/AiNetLinter/Foo.cs", false)]
    [InlineData("src/AiNetLinter.TestsOther/Foo.cs", false)]
    public void IsTestFile_IdentifiesTestFilesCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, PathNormalizer.IsTestFile(path));
    }
}
