#nullable enable

using System;
using AiNetLinter.Mcp.Handoffs;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class HandoffCounterAlphabetTests
{
    [Theory]
    [InlineData("a", "b")]
    [InlineData("b", "c")]
    [InlineData("y", "z")]
    [InlineData("z", "0")]
    [InlineData("0", "1")]
    [InlineData("8", "9")]
    [InlineData("9", "A")]
    [InlineData("A", "B")]
    [InlineData("Y", "Z")]
    [InlineData("Z", "aa")]
    [InlineData("aa", "ab")]
    [InlineData("aZ", "ba")]
    [InlineData("ZZ", "aaa")]
    [InlineData("aaZaZ", "aaZba")]
    public void GetNext_FollowsDefinedBase62SequenceAndCarries(string current, string expected)
    {
        var next = HandoffCounterAlphabet.GetNext(current);
        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("A")]
    [InlineData("Z")]
    [InlineData("aaZaZ")]
    [InlineData("Abc012XYZ")]
    public void IsValidCounter_AcceptsValidBase62Characters(string counter)
    {
        Assert.True(HandoffCounterAlphabet.IsValidCounter(counter));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("a_b")]
    [InlineData("x:b")]
    [InlineData("h:a")]
    [InlineData("äöü")]
    [InlineData("!")]
    public void IsValidCounter_RejectsInvalidCharactersAndEmpty(string? counter)
    {
        Assert.False(HandoffCounterAlphabet.IsValidCounter(counter));
    }

    [Theory]
    [InlineData("h:a")]
    [InlineData("h:z")]
    [InlineData("h:0")]
    [InlineData("h:A")]
    [InlineData("h:Z")]
    [InlineData("h:aa")]
    [InlineData("h:aaZaZ")]
    public void IsValidHandle_AcceptsCorrectHandles(string handle)
    {
        Assert.True(HandoffCounterAlphabet.IsValidHandle(handle));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("h")]
    [InlineData("h:")]
    [InlineData("h: ")]
    [InlineData("h:a ")]
    [InlineData("h:a!")]
    [InlineData("i:0")]
    [InlineData("x:a")]
    [InlineData("H:a")]
    public void IsValidHandle_RejectsMalformedHandles(string? handle)
    {
        Assert.False(HandoffCounterAlphabet.IsValidHandle(handle));
    }

    [Theory]
    [InlineData(@"h:\foo\bar.cs", true)]
    [InlineData("h:/foo/bar.cs", true)]
    [InlineData(@"C:\project\file.cs:10:5", true)]
    [InlineData("c:/project/file.cs", true)]
    [InlineData("h:a", false)]
    [InlineData("h:aaZaZ", false)]
    [InlineData("x:foo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWindowsDrivePath_IdentifiesDrivePathsCorrectly(string? input, bool expected)
    {
        Assert.Equal(expected, HandoffCounterAlphabet.IsWindowsDrivePath(input));
    }

    [Fact]
    public void TryExtractCounter_ExtractsCounterFromValidHandle()
    {
        Assert.True(HandoffCounterAlphabet.TryExtractCounter("h:aaZaZ", out var counter));
        Assert.Equal("aaZaZ", counter);

        Assert.False(HandoffCounterAlphabet.TryExtractCounter("invalid", out var empty));
        Assert.Equal(string.Empty, empty);
    }

    [Fact]
    public void GetNext_ThrowsOnEmptyOrInvalidCharacter()
    {
        Assert.Throws<ArgumentException>(() => HandoffCounterAlphabet.GetNext(""));
        Assert.Throws<ArgumentException>(() => HandoffCounterAlphabet.GetNext("a!b"));
    }
}
