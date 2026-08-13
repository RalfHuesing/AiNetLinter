#nullable enable

using System;

namespace AiNetLinter.FastTests.Mcp;

internal static class CompileErrorHeaderAssertions
{
    public static void AssertStartsWithCompileErrorHeader(string text, int expectedFileCount)
    {
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);
        var expected = expectedFileCount == 1
            ? "1 Datei hat Compile-Fehler"
            : $"{expectedFileCount} Dateien haben Compile-Fehler";
        Assert.Contains(expected, text, StringComparison.Ordinal);
    }
}
