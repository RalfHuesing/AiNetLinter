#nullable enable

using System;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

// @covers AssemblyFileFilter
[Trait("Category", "Unit")]
public sealed class AssemblyFileFilterTests
{
    [Theory]
    [InlineData("*.cs", "Foo.cs", true)]
    [InlineData("*.cs", "SubDir/Foo.cs", true)]
    [InlineData("*.cs", "Foo.txt", false)]
    [InlineData("!*Designer*", "Form.cs", true)]
    [InlineData("!*Designer*", "Form.Designer.cs", false)]
    [InlineData("SubDir/*", "SubDir/File.cs", true)]
    [InlineData("SubDir/*", "Other/File.cs", false)]
    public void IsMatch_GlobPatterns_MatchesCorrectly(string pattern, string path, bool expected)
    {
        var filter = AssemblyFileFilter.Create(pattern, "fileFilter");
        Assert.NotNull(filter);
        Assert.Equal(expected, filter!.IsMatch(path));
    }

    [Fact]
    public void Create_NullOrWhiteSpace_ReturnsNull()
    {
        Assert.Null(AssemblyFileFilter.Create(null, "fileFilter"));
        Assert.Null(AssemblyFileFilter.Create("   ", "fileFilter"));
        Assert.Null(AssemblyFileFilter.Create("!", "fileFilter"));
    }

    [Fact]
    public void Create_InvalidRegex_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => AssemblyFileFilter.Create("[unclosed", "fileFilter"));
        Assert.Contains("fileFilter", ex.Message);
    }
}
