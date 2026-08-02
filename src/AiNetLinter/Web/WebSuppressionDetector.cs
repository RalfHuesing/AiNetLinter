#nullable enable

using System;

namespace AiNetLinter.Web;

internal static class WebSuppressionDetector
{
    public static bool IsSuppressed(string? fileContent, string ruleName, AiNetLinter.Suppression.IgnoreSuppressionsFilter? ignoreFilter = null, string? languageKind = null)
    {
        if (string.IsNullOrEmpty(fileContent)) return false;
        if (string.IsNullOrEmpty(ruleName)) return false;

        var lang = languageKind ?? InferLanguageKind(ruleName);
        if (ignoreFilter != null && ignoreFilter.ShouldIgnoreSuppression(lang))
        {
            return false;
        }

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
