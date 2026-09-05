#nullable enable

using System.IO;
using AiNetLinter.Mcp.Tools.Common;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Common;

[Trait("Category", "Unit")]
public sealed class McpInputNormalizerTests
{
    [Theory]
    [InlineData("`MyClass`", "MyClass")]
    [InlineData("\"MyClass\"", "MyClass")]
    [InlineData("'MyClass'", "MyClass")]
    [InlineData("  `MyClass`  ", "MyClass")]
    [InlineData("`src/File.cs`", "src/File.cs")]
    [InlineData("MyClass", "MyClass")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StripEnclosingQuotesAndBackticks_RemovesQuotesAndBackticks(string? input, string expected)
    {
        var result = McpInputNormalizer.StripEnclosingQuotesAndBackticks(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CalculateAsync()", "CalculateAsync")]
    [InlineData("CalculateAsync( )", "CalculateAsync")]
    [InlineData("`CalculateAsync()`", "CalculateAsync")]
    [InlineData("IRepository<T>", "IRepository")]
    [InlineData("`IRepository<T>`", "IRepository")]
    [InlineData("IRepository<Customer>", "IRepository")]
    [InlineData("Service.DoWork()", "Service.DoWork")]
    [InlineData("`Service.DoWork()`", "Service.DoWork")]
    public void NormalizeSymbolIdentifier_CleansParenthesesGenericsAndQuotes(string input, string expected)
    {
        var result = McpInputNormalizer.NormalizeSymbolIdentifier(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizePathOrScope_ConvertsAbsolutePathWithinRootToRelative()
    {
        var root = Path.GetFullPath(@"C:\Projects\MyApp");
        var absolutePath = Path.Combine(root, "src", "Core");

        var result = McpInputNormalizer.NormalizePathOrScope(absolutePath, root);

        Assert.Equal("src/Core", result);
    }

    [Fact]
    public void NormalizePathOrScope_RootEqualsPath_ReturnsDot()
    {
        var root = Path.GetFullPath(@"C:\Projects\MyApp");

        var result = McpInputNormalizer.NormalizePathOrScope(root, root);

        Assert.Equal(".", result);
    }

    [Fact]
    public void NormalizePathOrScope_StripsBackticksAndNormalizesSlashes()
    {
        var result = McpInputNormalizer.NormalizePathOrScope("`src\\Services\\Worker.cs`");

        Assert.Equal("src/Services/Worker.cs", result);
    }
}
