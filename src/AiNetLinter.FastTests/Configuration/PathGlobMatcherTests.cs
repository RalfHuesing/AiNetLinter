#nullable enable

using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Unit")]
public sealed class PathGlobMatcherTests
{
    [Theory]
    [InlineData("Readme.md", "*.md", true)]
    [InlineData("Readme.md", "R?adme.md", true)]
    [InlineData("Readme.md", "R?adme.cs", false)]
    [InlineData("src/Readme.md", "*.md", false)]
    public void Matches_SingleSegmentWildcards_RespectSegmentBoundaries(
        string input,
        string pattern,
        bool expected)
    {
        Assert.Equal(expected, PathGlobMatcher.Matches(input, pattern));
    }

    [Theory]
    [InlineData("Readme.md", "**/Readme.md", true)]
    [InlineData("docs/Readme.md", "**/Readme.md", true)]
    [InlineData("src/docs/Readme.md", "src/**/Readme.md", true)]
    [InlineData("src/docs/Readme.md", "src/*.md", false)]
    public void Matches_DoubleStar_CrossesPathSegments(
        string input,
        string pattern,
        bool expected)
    {
        Assert.Equal(expected, PathGlobMatcher.Matches(input, pattern));
    }

    [Theory]
    [InlineData("SRC\\Docs\\README.MD", "src/docs/readme.md")]
    [InlineData("src\\Docs\\README.MD", "src/**/readme.md")]
    public void Matches_NormalizesSeparatorsAndIgnoresCase(string input, string pattern)
    {
        Assert.True(PathGlobMatcher.Matches(input, pattern));
    }

    [Theory]
    [InlineData("", "*.md")]
    [InlineData("Readme.md", "")]
    public void Matches_EmptyInputOrPattern_ReturnsFalse(string input, string pattern)
    {
        Assert.False(PathGlobMatcher.Matches(input, pattern));
    }
}
