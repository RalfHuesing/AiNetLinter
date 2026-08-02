using Xunit;
using AiNetLinter.Suppression;
using AiNetLinter.Web;

namespace AiNetLinter.Tests.Suppression;

// @covers IgnoreSuppressionsFilter
public sealed class IgnoreSuppressionsFilterTests
{
    [Fact]
    public void IgnoreSuppressionsFilter_Inactive_DoesNotIgnoreAnyLanguage()
    {
        var filter = IgnoreSuppressionsFilter.None;

        // Act & Assert
        Assert.False(filter.IsActive);
        Assert.False(filter.ShouldIgnoreSuppression("cs"));
        Assert.False(filter.ShouldIgnoreSuppression("razor"));
        Assert.False(filter.ShouldIgnoreSuppression("js"));
        Assert.False(filter.ShouldIgnoreSuppression("css"));
    }

    [Fact]
    public void IgnoreSuppressionsFilter_CsLanguage_BypassesCsOnly()
    {
        var filter = new IgnoreSuppressionsFilter(new[] { "c#", "razor" });

        // Act & Assert
        Assert.True(filter.IsActive);
        Assert.True(filter.ShouldIgnoreSuppression("cs"));
        Assert.True(filter.ShouldIgnoreSuppression("c#"));
        Assert.True(filter.ShouldIgnoreSuppression("razor"));
        Assert.False(filter.ShouldIgnoreSuppression("js"));
        Assert.False(filter.ShouldIgnoreSuppression("css"));

        Assert.True(filter.ShouldIgnoreSuppressionForFile("Test.cs"));
        Assert.True(filter.ShouldIgnoreSuppressionForFile("Component.razor"));
        Assert.False(filter.ShouldIgnoreSuppressionForFile("script.js"));
        Assert.False(filter.ShouldIgnoreSuppressionForFile("style.css"));
    }

    [Fact]
    public void IgnoreSuppressionsFilter_AllLanguages_BypassesAllLanguages()
    {
        var filter = new IgnoreSuppressionsFilter(new[] { "all" });

        // Act & Assert
        Assert.True(filter.IsActive);
        Assert.True(filter.ShouldIgnoreSuppression("cs"));
        Assert.True(filter.ShouldIgnoreSuppression("razor"));
        Assert.True(filter.ShouldIgnoreSuppression("js"));
        Assert.True(filter.ShouldIgnoreSuppression("css"));
        Assert.Equal(4, filter.ActiveLanguages.Count);
    }

    [Fact]
    public void SuppressionEvaluator_WithIgnoreFilter_ReturnsNotSuppressed()
    {
        var content = "// ainetlinter-disable EnforceNullableEnable\nclass Foo {}";
        var activeFilter = new IgnoreSuppressionsFilter(new[] { "cs" });
        var inactiveFilter = IgnoreSuppressionsFilter.None;

        bool suppressedWithActive = SuppressionEvaluator.IsSuppressed(content, "EnforceNullableEnable", 1, activeFilter);
        bool suppressedWithInactive = SuppressionEvaluator.IsSuppressed(content, "EnforceNullableEnable", 1, inactiveFilter);

        Assert.False(suppressedWithActive);
        Assert.True(suppressedWithInactive);
    }

    [Fact]
    public void WebSuppressionDetector_WithIgnoreFilter_ReturnsNotSuppressed()
    {
        var jsContent = "// ainetlinter-disable JS_MaxJsLineCount\nconsole.log('hi');";
        var cssContent = "/* ainetlinter-disable CSS_MaxCssLineCount */\nbody { color: red; }";

        var filter = new IgnoreSuppressionsFilter(new[] { "js" });

        bool jsSuppressed = WebSuppressionDetector.IsSuppressed(jsContent, "JS_MaxJsLineCount", filter, "js");
        bool cssSuppressed = WebSuppressionDetector.IsSuppressed(cssContent, "CSS_MaxCssLineCount", filter, "css");

        Assert.False(jsSuppressed); // JS ignore filter is active
        Assert.True(cssSuppressed); // CSS ignore filter is NOT active
    }

    [Fact]
    public void DisableAllDetector_WithIgnoreFilter_ReturnsFalseForDisableAll()
    {
        var content = "// ainetlinter-disable all\nclass Bar {}";
        var filter = new IgnoreSuppressionsFilter(new[] { "cs" });

        bool hasDisableAll = DisableAllDetector.HasDisableAll(content, filter, "cs");

        Assert.False(hasDisableAll);
    }
}
