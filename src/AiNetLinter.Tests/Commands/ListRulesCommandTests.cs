#nullable enable

using Xunit;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Output;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests fuer <see cref="ListRulesCommand"/>.
/// </summary>
public sealed class ListRulesCommandTests
{
    [Fact]
    public void ListAll_ReturnsZero()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.ListAll(console);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ListAll_OutputContainsKnownRuleIds()
    {
        var console = new TestLintConsole();
        ListRulesCommand.ListAll(console);
        var output = string.Join("\n", console.Output);

        Assert.Contains("MaxLineCount", output);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("EnforceSealedClasses", output);
    }

    [Fact]
    public void ListAll_OutputContainsTableHeader()
    {
        var console = new TestLintConsole();
        ListRulesCommand.ListAll(console);
        var output = string.Join("\n", console.Output);

        Assert.Contains("RuleId", output);
        Assert.Contains("Intent", output);
        Assert.Contains("Severity", output);
    }

    [Fact]
    public void DescribeOne_KnownRule_ReturnsZeroAndDetails()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.DescribeOne("EnforceNullableEnable", console);
        var output = string.Join("\n", console.Output);

        Assert.Equal(0, result);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("Warum", output);
        Assert.Contains("Fix-Alternativen", output);
    }

    [Fact]
    public void DescribeOne_UnknownRule_ReturnsOneAndError()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.DescribeOne("KeineEchteRegel", console);

        Assert.Equal(1, result);
        Assert.Single(console.Errors);
        Assert.Contains("KeineEchteRegel", console.Errors[0]);
    }

    [Fact]
    public void DescribeOne_CaseInsensitive_FindsRule()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.DescribeOne("enforcenullableenable", console);
        Assert.Equal(0, result);
        Assert.Empty(console.Errors);
    }

    [Fact]
    public void Search_MatchingTerm_ReturnsResultsWithCount()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.Search("nullable", console);
        var output = string.Join("\n", console.Output);

        Assert.Equal(0, result);
        Assert.Contains("EnforceNullableEnable", output);
        Assert.Contains("Treffer", output);
    }

    [Fact]
    public void Search_NoMatch_ReturnsZeroWithMessage()
    {
        var console = new TestLintConsole();
        var result = ListRulesCommand.Search("xyzNotARealKeyword42", console);
        var output = string.Join("\n", console.Output);

        Assert.Equal(0, result);
        Assert.Contains("Keine Regeln gefunden", output);
    }

    [Fact]
    public void Search_AgentContextIntent_FindsMultipleRules()
    {
        var console = new TestLintConsole();
        ListRulesCommand.Search("agent-context", console);
        var output = string.Join("\n", console.Output);

        Assert.Contains("MaxLineCount", output);
        Assert.Contains("MaxMethodLineCount", output);
    }
}
