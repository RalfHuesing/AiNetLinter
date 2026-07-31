using Xunit;
using System.IO;
using System.Linq;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Suppression;
using AiNetLinter.Web;

namespace AiNetLinter.Tests.Cli;

// @covers LinterArgs
// @covers IgnoreSuppressionsFilter
// @covers SuppressionEvaluator
// @covers WebSuppressionDetector
public sealed class IgnoreSuppressionsIntegrationTests
{
    [Fact]
    public void IgnoreSuppressions_OffByDefault_SuppressesViolations()
    {
        // Arrange
        var content = "// ainetlinter-disable EnforceNullableEnable\npublic class TestClass {}";
        var filter = IgnoreSuppressionsFilter.None;

        // Act
        bool isSuppressed = SuppressionEvaluator.IsSuppressed(content, "EnforceNullableEnable", 2, filter);

        // Assert
        Assert.True(isSuppressed);
    }

    [Fact]
    public void IgnoreSuppressions_CsActive_BypassesCsSuppression()
    {
        // Arrange
        var content = "// ainetlinter-disable EnforceNullableEnable\npublic class TestClass {}";
        var filter = new IgnoreSuppressionsFilter(new[] { "cs" });

        // Act
        bool isSuppressed = SuppressionEvaluator.IsSuppressed(content, "EnforceNullableEnable", 2, filter);

        // Assert
        Assert.False(isSuppressed);
    }

    [Fact]
    public void IgnoreSuppressions_WebFiles_BypassesSelectedWebLanguages()
    {
        // Arrange
        var jsContent = "// ainetlinter-disable JS_MaxJsLineCount\n" + string.Join('\n', Enumerable.Repeat("console.log('line');", 160));
        var cssContent = "/* ainetlinter-disable CSS_MaxCssLineCount */\n" + string.Join('\n', Enumerable.Repeat(".rule { color: red; }", 310));

        var jsFilter = new IgnoreSuppressionsFilter(new[] { "js" });
        var allFilter = new IgnoreSuppressionsFilter(new[] { "all" });

        // Act
        bool jsSuppressedWithJsFilter = WebSuppressionDetector.IsSuppressed(jsContent, "JS_MaxJsLineCount", jsFilter, "js");
        bool cssSuppressedWithJsFilter = WebSuppressionDetector.IsSuppressed(cssContent, "CSS_MaxCssLineCount", jsFilter, "css");

        bool jsSuppressedWithAllFilter = WebSuppressionDetector.IsSuppressed(jsContent, "JS_MaxJsLineCount", allFilter, "js");
        bool cssSuppressedWithAllFilter = WebSuppressionDetector.IsSuppressed(cssContent, "CSS_MaxCssLineCount", allFilter, "css");

        // Assert
        Assert.False(jsSuppressedWithJsFilter);
        Assert.True(cssSuppressedWithJsFilter);

        Assert.False(jsSuppressedWithAllFilter);
        Assert.False(cssSuppressedWithAllFilter);
    }
}
