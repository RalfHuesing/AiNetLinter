#nullable enable

using System;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Unit")]
public sealed class McpBatchArgumentsTests
{
    [Fact]
    public void Normalize_Null_ReturnsEmptyList()
    {
        var result = McpBatchArguments.Normalize(null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_EmptyArray_ReturnsEmptyList()
    {
        var result = McpBatchArguments.Normalize([]);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_OnlyWhitespaceEntries_ReturnsEmptyList()
    {
        var result = McpBatchArguments.Normalize(["", "   ", "\t"]);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_TrimsEntriesAndFiltersWhitespace()
    {
        var result = McpBatchArguments.Normalize(["  Greeter  ", "", "  GreetingService  "]);

        Assert.Equal(["Greeter", "GreetingService"], result);
    }

    [Fact]
    public void Normalize_DeduplicatesOrdinalByDefault()
    {
        var result = McpBatchArguments.Normalize(["Greeter", "Greeter", "greeter", "Greeter"]);

        Assert.Equal(["Greeter", "greeter"], result);
    }

    [Fact]
    public void Normalize_WithOrdinalIgnoreCase_DeduplicatesCaseInsensitively()
    {
        var result = McpBatchArguments.Normalize(
            ["src/MyClass.cs", "SRC/MYCLASS.CS", "src/Other.cs"],
            StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["src/MyClass.cs", "src/Other.cs"], result);
    }

    [Fact]
    public void Normalize_PreservesFirstOccurrenceOrder()
    {
        var result = McpBatchArguments.Normalize(["First", "Second", "First", "Third"]);

        Assert.Equal(["First", "Second", "Third"], result);
    }
}
