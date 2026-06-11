using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class SuppressionCommentParserTests
{
    [Theory]
    [InlineData("// ainetlinter-disable all", "EnforceSealedClasses", true)]
    [InlineData("// ainetlinter-disable ALL", "MaxLineCount", true)]
    [InlineData("// ainetlinter-disable", "MaxLineCount", true)]
    [InlineData("// ainetlinter-disable MaxLineCount", "MaxLineCount", true)]
    [InlineData("// ainetlinter-disable MaxLineCount", "EnforceSealedClasses", false)]
    public void MatchesRule_EvaluatesDisableComment(string line, string ruleName, bool expected)
    {
        var result = SuppressionCommentParser.MatchesRule(line, ruleName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ContainsDisableAll_DetectsExistingComment()
    {
        const string content = """
            // ainetlinter-disable all
            namespace Test;
            """;

        Assert.True(SuppressionCommentParser.ContainsDisableAll(content));
    }

    [Theory]
    [InlineData("// ainetlinter-disable all", true)]
    [InlineData("// ainetlinter-disable all\r", true)]
    [InlineData(" // ainetlinter-disable all", false)]
    [InlineData("// ainetlinter-disable all extra", false)]
    public void IsExactDisableAllLine_MatchesOnlyExactLine(string line, bool expected)
    {
        Assert.Equal(expected, SuppressionCommentParser.IsExactDisableAllLine(line));
    }

    [Fact]
    public void ContainsDisableAll_IgnoresRuleSpecificComment()
    {
        const string content = """
            // ainetlinter-disable MaxLineCount
            namespace Test;
            """;

        Assert.False(SuppressionCommentParser.ContainsDisableAll(content));
    }
}
