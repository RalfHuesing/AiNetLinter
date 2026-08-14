#nullable enable

using System;

namespace AiNetLinter.TestKit;

/// <summary>
/// Gemeinsame Assertions für MCP-Compile-Error-Header.
/// </summary>
public static class CompileErrorHeaderAssertions
{
    /// <summary>
    /// Prüft, ob der Text mit dem erwarteten Compile-Fehler-Header beginnt.
    /// </summary>
    public static void AssertStartsWithCompileErrorHeader(string text, int expectedFileCount)
    {
        if (!text.StartsWith("Hinweis:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected text to start with 'Hinweis:', but got: {text}");
        }

        var expected = expectedFileCount == 1
            ? "1 Datei hat Compile-Fehler"
            : $"{expectedFileCount} Dateien haben Compile-Fehler";
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected text to contain '{expected}', but got: {text}");
        }
    }
}
