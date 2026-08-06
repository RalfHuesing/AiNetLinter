#nullable enable

using System;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Geteilte Assertion-Helfer fuer die aggregierte Compile-Fehler-Warnung der MCP-Tools.
/// Stellt sicher, dass Singular ("1 Datei hat") und Plural ("N Dateien haben") jeweils
/// exakt geprueft werden — eine reine Regex-Match-Variante trifft die Singular-Form
/// nicht zuverlaessig.
/// </summary>
internal static class CompileErrorHeaderAssertions
{
    /// <summary>
    /// Prueft, dass <paramref name="text"/> mit der aggregierten Compile-Fehler-Warnung
    /// beginnt und die Singular- bzw. Plural-Form exakt zur Anzahl
    /// <paramref name="expectedFileCount"/> passt.
    /// </summary>
    public static void AssertStartsWithCompileErrorHeader(string text, int expectedFileCount)
    {
        Assert.StartsWith("Hinweis:", text, StringComparison.Ordinal);

        var expected = expectedFileCount == 1
            ? "1 Datei hat Compile-Fehler"
            : $"{expectedFileCount} Dateien haben Compile-Fehler";

        Assert.Contains(expected, text, StringComparison.Ordinal);
    }
}
