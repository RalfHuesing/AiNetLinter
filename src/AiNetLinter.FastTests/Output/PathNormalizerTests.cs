#nullable enable

using System.IO;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.FastTests.Output;

[Trait("Category", "Unit")]
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
    [InlineData("")]
    [InlineData("   ")]
    public void ToRelative_ReturnsFileNameWhenOutputRootIsNullOrEmpty(string outputRoot)
    {
        var absolute = Path.GetFullPath(@"C:\Projects\MyApp\src\Foo.cs");

        Assert.Equal("Foo.cs", PathNormalizer.ToRelative(outputRoot, absolute));
    }

    [Theory]
    [InlineData("src/AiNetLinter.Tests/Foo.cs", true)]
    [InlineData("src\\AiNetLinter.Tests\\Foo.cs", true)]
    [InlineData("src/AiNetLinter.FastTests/Foo.cs", true)]
    [InlineData("src\\AiNetLinter.FastTests\\Foo.cs", true)]
    [InlineData("src/AiNetLinter.IntegrationTests/Foo.cs", true)]
    [InlineData("src\\AiNetLinter.IntegrationTests\\Foo.cs", true)]
    [InlineData("src/AiNetLinter.TestKit/Foo.cs", true)]
    [InlineData("src\\AiNetLinter.TestKit\\Foo.cs", true)]
    [InlineData("src/AiNetLinter.tests/Foo.cs", true)]
    [InlineData("src/AiNetLinter/Foo.cs", false)]
    [InlineData("src/AiNetLinter.TestsOther/Foo.cs", false)]
    public void IsTestFile_IdentifiesTestFilesCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, PathNormalizer.IsTestFile(path));
    }

    [Theory]
    [InlineData(@"src\AiNetLinter\Mcp\Tool.cs", "src/AiNetLinter/Mcp/Tool.cs")]
    [InlineData("src/AiNetLinter/Mcp/Tool.cs", "src/AiNetLinter/Mcp/Tool.cs")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeSeparators_ConvertsBackslashesToForwardSlashes(string? input, string expected)
    {
        Assert.Equal(expected, PathNormalizer.NormalizeSeparators(input));
    }

    [Theory]
    [InlineData(@"src\AiNetLinter\Mcp\Tool.cs", "src/AiNetLinter/Mcp", true)]
    [InlineData("src/AiNetLinter/Mcp/Tool.cs", @"src\AiNetLinter\Mcp", true)]
    [InlineData(@"C:\Solution\src\AiNetLinter\Mcp\Tool.cs", "src/AiNetLinter/Mcp", true)]
    [InlineData("src/AiNetLinter/Mcp/Tool.cs", "OtherDir", false)]
    [InlineData("src/AiNetLinter/Mcp/Tool.cs", "", true)]
    [InlineData("src/AiNetLinter/Mcp/Tool.cs", null, true)]
    [InlineData("", "src/Mcp", false)]
    [InlineData(null, "src/Mcp", false)]
    public void MatchesScope_MatchesCrossPlatformPathsCorrectly(string? filePath, string? scopeFilter, bool expected)
    {
        Assert.Equal(expected, PathNormalizer.MatchesScope(filePath, scopeFilter));
    }
}
