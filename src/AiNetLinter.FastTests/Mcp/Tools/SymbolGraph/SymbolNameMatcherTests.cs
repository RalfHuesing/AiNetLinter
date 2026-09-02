#nullable enable

using AiNetLinter.Mcp.Tools.SymbolGraph;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Unit")]
public sealed class SymbolNameMatcherTests
{
    [Theory]
    [InlineData("ExecuteAsync()", "ExecuteAsync")]
    [InlineData("  Run()  ", "Run")]
    [InlineData("MyProperty", "MyProperty")]
    public void CleanPattern_StripsParenthesesAndTrims(string raw, string expected)
    {
        Assert.Equal(expected, SymbolNameMatcher.CleanPattern(raw));
    }

    [Theory]
    [InlineData("*MagicValues*", "MagicValuesStringHeuristics", true)]
    [InlineData("*MagicValues*", "FindMagicValuesTool", true)]
    [InlineData("*MagicValues*", "OtherClass", false)]
    [InlineData("Inspect*Tests", "InspectAssemblyToolTests", true)]
    [InlineData("Inspect*Tests", "InspectTests", true)]
    [InlineData("Inspect*Tests", "InspectAssemblyTool", false)]
    [InlineData("*Scanner", "FindSymbolScanner", true)]
    [InlineData("*Scanner", "Scanner", true)]
    [InlineData("*Scanner", "ScannerHelper", false)]
    [InlineData("Get*", "GetCallTreeTool", true)]
    [InlineData("Get*", "SetCallTreeTool", false)]
    public void CreateDeclarationNameFilter_Wildcards_MatchCorrectly(string pattern, string symbolName, bool expected)
    {
        var filter = SymbolNameMatcher.CreateDeclarationNameFilter(pattern);
        Assert.Equal(expected, filter(symbolName));
    }

    [Fact]
    public void CreateDeclarationNameFilter_DottedName_MatchesLastAndSecondLastParts()
    {
        var filter = SymbolNameMatcher.CreateDeclarationNameFilter("FindSymbolTool.ExecuteAsync");
        Assert.True(filter("ExecuteAsync"));
        Assert.True(filter("FindSymbolTool"));
        Assert.False(filter("OtherClass"));
    }
}
