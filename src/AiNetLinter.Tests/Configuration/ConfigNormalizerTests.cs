using AiNetLinter.Configuration;

namespace AiNetLinter.Tests.Configuration;

public sealed class ConfigNormalizerTests
{
    private static Config CreateBaseConfig()
    {
        _ = typeof(GlobalConfig);
        _ = typeof(MetricsConfig);
        _ = typeof(TestSentinelConfig);
        _ = typeof(UiSeparationConfig);

        return new()
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
        };
    }

    [Fact]
    public void Normalize_RestoresDefaultPatterns_WhenClassNamePatternsIsNull()
    {
        var config = CreateBaseConfig() with
        {
            TestSentinel = new TestSentinelConfig { ClassNamePatterns = null! },
        };

        var normalized = ConfigNormalizer.Normalize(config);

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
            ConfigNormalizer.Normalize(config));

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
            ConfigNormalizer.Normalize(config));

        Assert.Contains("{Name}", exception.Message, StringComparison.Ordinal);
    }
}
