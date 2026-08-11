#nullable enable

namespace AiNetLinter.Web;

/// <summary>
/// Zeilenzaehlung fuer Nicht-Roslyn-Textinhalte (CSS/JS/Razor) — zentral statt separat in
/// <see cref="CssAnalyzer"/>, <see cref="JsAnalyzer"/> und <see cref="RazorAnalyzer"/> dupliziert.
/// </summary>
internal static class WebTextMetrics
{
    internal static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var n = 1;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n') n++;
        }
        return n;
    }
}
