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
internal static class WebSuppressionDetector
{
    /// <summary>
    /// Prueft, ob eine Regel in der Datei unterdrueckt wird.
    /// </summary>
    /// <param name="fileContent">Vollstaendiger Datei-Inhalt (kann null/leer sein).</param>
    /// <param name="ruleName">Regel-ID (z. B. "CSS_MaxCssLineCount").</param>
    /// <param name="ignoreFilter">Optionaler Bypass-Filter.</param>
    /// <param name="languageKind">Optionale explizite Sprachklasse ("css", "js", "razor"). Falls null, wird sie aus der RuleId abgeleitet.</param>
    public static bool IsSuppressed(string? fileContent, string ruleName, AiNetLinter.Suppression.IgnoreSuppressionsFilter? ignoreFilter = null, string? languageKind = null)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;
        if (string.IsNullOrEmpty(ruleName)) return false;

        var lang = languageKind ?? InferLanguageKind(ruleName);
        if (ignoreFilter != null && ignoreFilter.ShouldIgnoreSuppression(lang))
        {
            return false;
        }

        // Globaler Disable-all-Schalter.
        if (ContainsIgnoreCase(fileContent, "ainetlinter-disable all")) return true;

        return ContainsIgnoreCase(fileContent, $"ainetlinter-disable {ruleName}");
    }

    private static string InferLanguageKind(string ruleName) =>
        ruleName switch
        {
            var r when r.StartsWith("CSS_", StringComparison.OrdinalIgnoreCase) => "css",
            var r when r.StartsWith("JS_", StringComparison.OrdinalIgnoreCase) => "js",
            var r when r.StartsWith("RAZOR_", StringComparison.OrdinalIgnoreCase) => "razor",
            _ => "cs"
        };

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
