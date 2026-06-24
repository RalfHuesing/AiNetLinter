#nullable enable

using System;

namespace AiNetLinter.Web;

/// <summary>
/// Parst Inline-Suppression-Kommentare in Web-Dateien (CSS/JS/Razor).
/// Syntax pro Dateityp:
/// - CSS:    /* ainetlinter-disable CSS_MaxCssLineCount */
/// - JS:     // ainetlinter-disable JS_MaxJsLineCount
/// - Razor:  @* ainetlinter-disable RAZOR_MaxRazorLineCount *@
/// Auch dateiweites Disable-all wird unterstuetzt: "ainetlinter-disable all".
/// </summary>
internal static class WebSuppressionHelper
{
    /// <summary>
    /// Prueft, ob eine Regel in der Datei unterdrueckt wird.
    /// </summary>
    /// <param name="fileContent">Vollstaendiger Datei-Inhalt (kann null/leer sein).</param>
    /// <param name="ruleName">Regel-ID (z. B. "CSS_MaxCssLineCount").</param>
    public static bool IsSuppressed(string? fileContent, string ruleName)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;
        if (string.IsNullOrEmpty(ruleName)) return false;

        // Globaler Disable-all-Schalter.
        if (ContainsIgnoreCase(fileContent, "ainetlinter-disable all")) return true;

        return ContainsIgnoreCase(fileContent, $"ainetlinter-disable {ruleName}");
    }

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
