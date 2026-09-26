using AiNetLinter.Configuration;

namespace AiNetLinter.FastTests.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigNormalizerTests
{
    private static Config CreateBaseConfig()
    {
        _ = typeof(GlobalConfig);
        _ = typeof(MetricsConfig);
        _ = typeof(TestSentinelConfig);
        _ = typeof(UiSeparationConfig);

        return TestHelper.CreateDefaultConfig();
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
    public void Normalize_RestoresClosedSolutionDeadCodePolicy_WhenSectionIsNull()
    {
        var normalized = ConfigNormalizer.Normalize(CreateBaseConfig() with { DeadCode = null! });

        Assert.Equal("closed_solution", normalized.DeadCode.DefaultApiSurface);
    }

    [Fact]
    public void Normalize_RestoresEmptyEntryPointAttributes_WhenListIsNull()
    {
        var config = CreateBaseConfig() with
        {
            DeadCode = new DeadCodeConfig { EntryPointAttributes = null! },
        };

        var normalized = ConfigNormalizer.Normalize(config);

        Assert.Empty(normalized.DeadCode.EntryPointAttributes);
    }

    [Fact]
    public void Normalize_TrimsAndDeduplicatesEntryPointAttributes()
    {
        var config = CreateBaseConfig() with
        {
            DeadCode = new DeadCodeConfig
            {
                EntryPointAttributes = [" Example.PluginEntryAttribute ", "Example.PluginEntryAttribute"],
            },
        };

        var normalized = ConfigNormalizer.Normalize(config);

        Assert.Equal(["Example.PluginEntryAttribute"], normalized.DeadCode.EntryPointAttributes);
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
