#nullable enable

using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

/// <summary>
/// Stellt sicher dass jede in RuleMetadataRegistry registrierte Regel einen expliziten
/// Legende-Eintrag in RuleLegendRegistry hat. Schlägt an wenn eine neue Regel hinzugefügt
/// wird ohne gleichzeitig Warum-Text und Fix-Alternativen zu ergänzen.
/// </summary>
public sealed class RuleLegendRegistryTests
{
    public static IEnumerable<object[]> AllKnownRuleNames =>
        RuleMetadataRegistry.KnownRuleNames.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(AllKnownRuleNames))]
    public void AllRegisteredRulesHaveExplicitLegendEntry(string ruleName)
    {
        Assert.True(
            RuleLegendRegistry.HasEntry(ruleName),
            $"Regel '{ruleName}' fehlt in RuleLegendRegistry — Warum-Text und Fix-Alternativen ergänzen.");
    }

    [Theory]
    [MemberData(nameof(AllKnownRuleNames))]
    public void AllLegendEntriesHaveNonEmptyContent(string ruleName)
    {
        var entry = RuleLegendRegistry.TryGet(ruleName);
        if (entry == null) return;

        Assert.False(string.IsNullOrWhiteSpace(entry.Warum),
            $"Regel '{ruleName}': Warum-Text ist leer.");
        Assert.NotEmpty(entry.Alternativen);
        Assert.All(entry.Alternativen, alt =>
            Assert.False(string.IsNullOrWhiteSpace(alt),
                $"Regel '{ruleName}': Leere Alternative gefunden."));
    }

    [Theory]
    [MemberData(nameof(AllKnownRuleNames))]
    public void RenderedLegendEntryContainsRuleName(string ruleName)
    {
        var rendered = RuleLegendRegistry.Render(ruleName, 1, "general");

        Assert.Contains(ruleName, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**Warum:**", rendered, StringComparison.Ordinal);
        Assert.Contains("**Fix-Alternativen:**", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_IncludesConfigKeyHintWhenPresent()
    {
        var rendered = RuleLegendRegistry.Render("MaxPartialClassFiles", 1, "agent-context");
        Assert.Contains("**Konfiguration:** `rules.json → Metrics.MaxPartialClassFiles", rendered);
    }

    [Fact]
    public void Render_OmitsConfigKeyHintWhenAbsent()
    {
        var rendered = RuleLegendRegistry.Render("MaxLineCount", 1, "agent-context");
        Assert.DoesNotContain("**Konfiguration:**", rendered);
    }
}
