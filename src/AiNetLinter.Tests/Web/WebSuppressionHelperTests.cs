#nullable enable

using AiNetLinter.Web;
using Xunit;

namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer WebSuppressionHelper. Verifiziert dateiweite und regel-spezifische
/// Suppression-Kommentare in Web-Dateien.
/// </summary>
public sealed class WebSuppressionHelperTests
{
    [Fact]
    public void IsSuppressed_ReturnsTrue_WhenDisableAllPresent()
    {
        const string content = """
            /* ainetlinter-disable all */
            .card { color: red; }
            """;

        Assert.True(WebSuppressionHelper.IsSuppressed(content, "CSS_MaxCssLineCount"));
        Assert.True(WebSuppressionHelper.IsSuppressed(content, "CSS_PreferScopedCss"));
        Assert.True(WebSuppressionHelper.IsSuppressed(content, "CSS_MaxCssSelectorComplexity"));
    }

    [Fact]
    public void IsSuppressed_ReturnsTrue_WhenSpecificRulePresent()
    {
        const string content = """
            /* ainetlinter-disable CSS_MaxCssLineCount */
            .card { color: red; }
            """;

        Assert.True(WebSuppressionHelper.IsSuppressed(content, "CSS_MaxCssLineCount"));
        Assert.False(WebSuppressionHelper.IsSuppressed(content, "CSS_PreferScopedCss"));
    }

    [Fact]
    public void IsSuppressed_ReturnsFalse_WhenNoCommentPresent()
    {
        const string content = """
            .card { color: red; }
            """;

        Assert.False(WebSuppressionHelper.IsSuppressed(content, "CSS_MaxCssLineCount"));
    }

    [Fact]
    public void IsSuppressed_ReturnsFalse_WhenContentIsEmpty()
    {
        Assert.False(WebSuppressionHelper.IsSuppressed("", "CSS_MaxCssLineCount"));
        Assert.False(WebSuppressionHelper.IsSuppressed(null, "CSS_MaxCssLineCount"));
    }

    [Fact]
    public void IsSuppressed_CaseInsensitive()
    {
        const string content = """
            /* AINETLINTER-DISABLE CSS_MaxCssLineCount */
            .card { color: red; }
            """;

        Assert.True(WebSuppressionHelper.IsSuppressed(content, "CSS_MaxCssLineCount"));
    }

    [Fact]
    public void IsSuppressed_ReturnsFalse_WhenRuleNameIsEmpty()
    {
        const string content = """
            .card { color: red; }
            """;

        Assert.False(WebSuppressionHelper.IsSuppressed(content, ""));
    }
}