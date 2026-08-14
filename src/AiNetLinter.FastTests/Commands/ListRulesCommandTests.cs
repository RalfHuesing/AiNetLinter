#nullable enable

using Xunit;
using AiNetLinter.Commands;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Commands;

/// <summary>
/// Tests fuer <see cref="ListRulesCommand"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ListRulesCommandTests
{
    [Fact]
    public void ListAll_ReturnsZero()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.ListAll(console);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ListAll_OutputContainsKnownRuleIds()
    {
        var console = new RecordingLintConsole();
        ListRulesCommand.ListAll(console);
        var output = console.OutputText;

        Assert.Contains("MaxLineCount", output);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("EnforceSealedClasses", output);
    }

    [Fact]
    public void ListAll_OutputContainsTableHeader()
    {
        var console = new RecordingLintConsole();
        ListRulesCommand.ListAll(console);
        var output = console.OutputText;

        Assert.Contains("RuleId", output);
        Assert.Contains("Intent", output);
        Assert.Contains("Severity", output);
    }

    [Fact]
    public void DescribeOne_KnownRule_ReturnsZeroAndDetails()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.DescribeOne("EnforceNullableEnable", console);
        var output = console.OutputText;

        Assert.Equal(0, result);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("Warum", output);
        Assert.Contains("Fix-Alternativen", output);
    }

    [Fact]
    public void DescribeOne_UnknownRule_ReturnsOneAndError()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.DescribeOne("KeineEchteRegel", console);

        Assert.Equal(1, result);
        Assert.Contains("KeineEchteRegel", console.ErrorText);
    }

    [Fact]
    public void DescribeOne_CaseInsensitive_FindsRule()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.DescribeOne("enforcenullableenable", console);
        Assert.Equal(0, result);
        Assert.Empty(console.ErrorText);
    }

    [Fact]
    public void Search_MatchingTerm_ReturnsResultsWithCount()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.Search("nullable", console);
        var output = console.OutputText;

        Assert.Equal(0, result);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("Treffer", output);
    }

    [Fact]
    public void Search_NoMatch_ReturnsZeroWithMessage()
    {
        var console = new RecordingLintConsole();
        var result = ListRulesCommand.Search("xyzNotARealKeyword42", console);
        var output = console.OutputText;

        Assert.Equal(0, result);
        Assert.Contains("Keine Regeln gefunden", output);
    }

    [Fact]
    public void Search_AgentContextIntent_FindsMultipleRules()
    {
        var console = new RecordingLintConsole();
        ListRulesCommand.Search("agent-context", console);
        var output = console.OutputText;

        Assert.Contains("MaxLineCount", output);
        Assert.Contains("MaxMethodLineCount", output);
    }
}
