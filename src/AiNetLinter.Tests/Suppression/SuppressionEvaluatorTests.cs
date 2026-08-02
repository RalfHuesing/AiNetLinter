using AiNetLinter.Suppression;

namespace AiNetLinter.Tests.Suppression;

public sealed class SuppressionEvaluatorTests
{
    [Fact]
    public void IsSuppressed_WithDisableAllAtFileStart_ReturnsTrueForAnyRule()
    {
        const string source = """
            // ainetlinter-disable all
            namespace Test;
            public sealed class Example {}
            """;

        Assert.True(SuppressionEvaluator.IsSuppressed(source, "StaticTestSentinel", 3));
        Assert.True(SuppressionEvaluator.IsSuppressed(source, "MaxInheritanceDepth", 3));
    }

    [Fact]
    public void IsSuppressed_WithRuleSpecificComment_ReturnsTrueOnlyForMatchingRule()
    {
        const string source = """
            // ainetlinter-disable StaticTestSentinel
            namespace Test;
            public sealed class Example {}
            """;

        Assert.True(SuppressionEvaluator.IsSuppressed(source, "StaticTestSentinel", 3));
        Assert.False(SuppressionEvaluator.IsSuppressed(source, "MaxInheritanceDepth", 3));
    }
}
