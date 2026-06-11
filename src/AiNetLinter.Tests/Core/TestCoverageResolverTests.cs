using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class TestCoverageResolverTests
{
    [Fact]
    public void IsCovered_SkipsNullTestClassName_InWildcardPattern()
    {
        var index = new TestCoverageIndex();
        index.AddTestClass(null!);

        var config = new TestSentinelConfig
        {
            ClassNamePatterns = ["{Name}*Tests"],
        };

        var covered = TestCoverageResolver.IsCovered("MyService", index, config);

        Assert.False(covered);
    }

    [Fact]
    public void IsCovered_ThrowsForEmptySourceClassName()
    {
        var index = new TestCoverageIndex();

        var exception = Assert.Throws<ArgumentException>(() =>
            TestCoverageResolver.IsCovered(string.Empty, index, new TestSentinelConfig()));

        Assert.Equal("sourceClassName", exception.ParamName);
    }

    [Fact]
    public void IsCovered_ThrowsForMissingClassNamePatterns()
    {
        var index = new TestCoverageIndex();
        var config = new TestSentinelConfig { ClassNamePatterns = [] };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestCoverageResolver.IsCovered("MyService", index, config));

        Assert.Contains("ClassNamePatterns", exception.Message, StringComparison.Ordinal);
    }
}
