#nullable enable

using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
// @covers AssemblyReferenceResolver
public sealed class AssemblyReferenceResolverTests
{
    [Theory]
    [InlineData("mscorlib")]
    [InlineData("System")]
    [InlineData("System.Runtime")]
    [InlineData("Microsoft.CodeAnalysis")]
    [InlineData("WindowsBase")]
    [InlineData("WindowsBase.Extensions")]
    public void IdentityMatches_FrameworkPrefixesIgnoreVersion(string name)
    {
        var expected = new AssemblyReferenceDto(name, "1.0.0.0", "neutral", false);
        var actual = new AssemblyIdentityDto(name, "9.9.9.9", "neutral", string.Empty);

        Assert.True(AssemblyReferenceResolver.IdentityMatches(expected, actual));
    }

    [Fact]
    public void IdentityMatches_ThirdPartyAssembliesKeepStrictVersionMatching()
    {
        var expected = new AssemblyReferenceDto("ThirdParty.Component", "1.0.0.0", "neutral", false);
        var actual = new AssemblyIdentityDto("ThirdParty.Component", "9.9.9.9", "neutral", string.Empty);

        Assert.False(AssemblyReferenceResolver.IdentityMatches(expected, actual));
    }

    [Fact]
    public void IdentityMatches_FrameworkPrefixesStillRequireMatchingCulture()
    {
        var expected = new AssemblyReferenceDto("System.Runtime", "1.0.0.0", "de-DE", false);
        var actual = new AssemblyIdentityDto("System.Runtime", "9.9.9.9", "neutral", string.Empty);

        Assert.False(AssemblyReferenceResolver.IdentityMatches(expected, actual));
    }

    [Fact]
    public void IdentityMatches_NonFrameworkPrefixDoesNotUnifyByNameSimilarity()
    {
        var expected = new AssemblyReferenceDto("Systemish.Component", "1.0.0.0", "neutral", false);
        var actual = new AssemblyIdentityDto("Systemish.Component", "9.9.9.9", "neutral", string.Empty);

        Assert.False(AssemblyReferenceResolver.IdentityMatches(expected, actual));
    }
}
