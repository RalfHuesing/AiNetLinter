using AiNetLinter.Configuration;

namespace AiNetLinter.Tests.Configuration;

public sealed class LinterConfigNormalizerTests
{
    private static LinterConfig CreateBaseConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };

    [Fact]
    public void Normalize_RestoresDefaultPatterns_WhenClassNamePatternsIsNull()
    {
        var config = CreateBaseConfig() with
        {
            TestSentinel = new TestSentinelConfig { ClassNamePatterns = null! },
        };

        var normalized = LinterConfigNormalizer.Normalize(config);

        Assert.Equal(4, normalized.TestSentinel.ClassNamePatterns.Count);
        Assert.Contains("{Name}Tests", normalized.TestSentinel.ClassNamePatterns);
    }

    [Fact]
    public void Normalize_ThrowsForNullPatternEntry()
    {
        var config = CreateBaseConfig() with
        {
            TestSentinel = new TestSentinelConfig { ClassNamePatterns = [null!] },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LinterConfigNormalizer.Normalize(config));

        Assert.Contains("ClassNamePatterns[0]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_ThrowsForPatternWithoutNamePlaceholder()
    {
        var config = CreateBaseConfig() with
        {
            TestSentinel = new TestSentinelConfig { ClassNamePatterns = ["*Tests"] },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LinterConfigNormalizer.Normalize(config));

        Assert.Contains("{Name}", exception.Message, StringComparison.Ordinal);
    }
}
