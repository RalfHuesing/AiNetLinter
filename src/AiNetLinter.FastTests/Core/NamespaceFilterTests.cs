#nullable enable

using System.Collections.Generic;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class NamespaceFilterTests
{
    [Fact]
    public void MatchesGlob_WithWildcard_MatchesCorrectly()
    {
        Assert.True(NamespaceFilter.MatchesGlob("San.Auth.Core", "San.Auth*"));
        Assert.True(NamespaceFilter.MatchesGlob("San.Auth.Core", "*Auth*"));
        Assert.True(NamespaceFilter.MatchesGlob("San.Auth.Core", "*.Core"));
        Assert.False(NamespaceFilter.MatchesGlob("San.Auth.Core", "*.Domain"));
    }

    [Fact]
    public void IsNamespaceAllowed_WithIncludesAndExcludes_FiltersCorrectly()
    {
        var includes = new List<string> { "San.Auth*" };
        var excludes = new List<string> { "*.Internal", "*Test*" };

        Assert.True(NamespaceFilter.IsNamespaceAllowed("San.Auth.Services", includes, excludes));
        Assert.False(NamespaceFilter.IsNamespaceAllowed("San.Auth.Internal", includes, excludes));
        Assert.False(NamespaceFilter.IsNamespaceAllowed("San.Auth.TestServices", includes, excludes));
        Assert.False(NamespaceFilter.IsNamespaceAllowed("San.Billing.Services", includes, excludes));
    }
}
