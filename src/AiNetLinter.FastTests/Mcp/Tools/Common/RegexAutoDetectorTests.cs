#nullable enable

using System;
using AiNetLinter.Mcp.Tools.Common;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Common;

[Trait("Category", "Unit")]
public sealed class RegexAutoDetectorTests
{
    [Theory]
    [InlineData(@"class\s+\w+")]
    [InlineData(@"\bExecuteAsync\b")]
    [InlineData(@"^public\s+void")]
    [InlineData(@"return\s+null;$")]
    [InlineData(@"(?i)mcp.*server")]
    [InlineData(@"(?:async\s+)?Task")]
    [InlineData(@"\d{3}-\d{2}")]
    public void IsLikelyRegex_WithClearRegexSyntax_ReturnsTrue(string pattern)
    {
        Assert.True(RegexAutoDetector.IsLikelyRegex(pattern));
    }

    [Theory]
    [InlineData("MyService")]
    [InlineData("public void Run()")]
    [InlineData("int[]")]
    [InlineData("new List<string>()")]
    [InlineData("x + y")]
    [InlineData("a || b")]
    [InlineData("x != null")]
    [InlineData("foo.bar")]
    [InlineData("")]
    [InlineData(null)]
    public void IsLikelyRegex_WithNormalCSharpCodeOrSimpleText_ReturnsFalse(string? pattern)
    {
        Assert.False(RegexAutoDetector.IsLikelyRegex(pattern));
    }

    [Theory]
    [InlineData(".*Service")]
    [InlineData(".+Handler")]
    [InlineData("[A-Z]foo")]
    [InlineData("a|b")]
    [InlineData("*Service*")]
    [InlineData("I?Repository")]
    public void HasRegexMetaCharacters_WithMetaChars_ReturnsTrue(string pattern)
    {
        Assert.True(RegexAutoDetector.HasRegexMetaCharacters(pattern));
    }

    [Theory]
    [InlineData("MyService")]
    [InlineData("ExecuteAsync")]
    [InlineData("")]
    [InlineData(null)]
    public void HasRegexMetaCharacters_WithoutMetaChars_ReturnsFalse(string? pattern)
    {
        Assert.False(RegexAutoDetector.HasRegexMetaCharacters(pattern));
    }

    [Fact]
    public void ConvertWildcardToRegex_ConvertsCorrectly()
    {
        var regexStr = RegexAutoDetector.ConvertWildcardToRegex("*Service?.cs");
        Assert.Equal(".*Service.\\.cs", regexStr);

        Assert.True(RegexAutoDetector.IsValidRegex(regexStr, out var regex));
        Assert.NotNull(regex);
        Assert.True(regex.IsMatch("MyService1.cs"));
        Assert.True(regex.IsMatch("Sub/Path/CustomerServiceA.cs"));
        Assert.False(regex.IsMatch("MyService12.cs"));
    }

    [Fact]
    public void ConvertWildcardToRegex_Anchored_WrapsWithAnchorsAndHandlesSeparators()
    {
        var regexStr = RegexAutoDetector.ConvertWildcardToRegex("src/*.cs", anchored: true);
        Assert.Equal("^src[/\\\\].*\\.cs$", regexStr);

        Assert.True(RegexAutoDetector.IsValidRegex(regexStr, out var regex));
        Assert.NotNull(regex);
        Assert.True(regex.IsMatch("src/Foo.cs"));
        Assert.True(regex.IsMatch(@"src\Foo.cs"));
        Assert.False(regex.IsMatch("other/src/Foo.cs"));
    }

    [Theory]
    [InlineData("*.cs", "Foo.cs", true)]
    [InlineData("*.cs", "sub/dir/Foo.cs", true)]
    [InlineData("*.cs", "Foo.txt", false)]
    [InlineData("!*Designer*", "Form.cs", true)]
    [InlineData("!*Designer*", "Form.Designer.cs", false)]
    public void TryCreateFilterRegex_HandlesGlobsAndNegation(string filter, string testPath, bool expected)
    {
        Assert.True(RegexAutoDetector.TryCreateFilterRegex(filter, out var regex, out var isNegated, out var errorMessage));
        Assert.Null(errorMessage);
        Assert.NotNull(regex);

        var isMatch = regex!.IsMatch(testPath);
        var effective = isNegated ? !isMatch : isMatch;
        Assert.Equal(expected, effective);
    }

    [Fact]
    public void TryCreateFilterRegex_NullOrEmpty_ReturnsTrueWithNullRegex()
    {
        Assert.True(RegexAutoDetector.TryCreateFilterRegex(null, out var regex1, out _, out _));
        Assert.Null(regex1);

        Assert.True(RegexAutoDetector.TryCreateFilterRegex("   ", out var regex2, out _, out _));
        Assert.Null(regex2);
    }

    [Fact]
    public void TryCreateFilterRegex_InvalidPattern_ReturnsFalseWithError()
    {
        Assert.False(RegexAutoDetector.TryCreateFilterRegex("[unclosed", out var regex, out _, out var error));
        Assert.Null(regex);
        Assert.NotNull(error);
        Assert.Contains("[unclosed", error);
    }
}
