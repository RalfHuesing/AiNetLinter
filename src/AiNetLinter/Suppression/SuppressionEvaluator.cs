#nullable enable

namespace AiNetLinter.Suppression;

/// <summary>
/// Prüft, ob Regelverstöße anhand von ainetlinter-disable-Kommentaren unterdrückt werden.
/// </summary>
public static class SuppressionEvaluator
{
    /// <summary>
    /// Prüft, ob ein Verstoß für die angegebene Regel unterdrückt ist.
    /// Ein passender Kommentar in beliebiger Zeile der Datei unterdrückt alle Verstöße dieser Regel.
    /// </summary>
    /// <param name="fileContent">Der gesamte Inhalt der Datei als Zeichenkette.</param>
    /// <param name="ruleName">Der Name der zu prüfenden Regel.</param>
    /// <param name="lineNumber">Die Zeilennummer des Verstoßes.</param>
    /// <returns>True, wenn der Verstoß unterdrückt ist; andernfalls False.</returns>
    public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
    {
        var lines = fileContent.Split('\n');

        foreach (var line in lines)
        {
            if (SuppressionCommentParser.MatchesRule(line, ruleName))
            {
                return true;
            }
        }

        if (lineNumber <= 0 || lineNumber > lines.Length)
        {
            return false;
        }

        return SuppressionCommentParser.MatchesRule(lines[lineNumber - 1], ruleName);
    }
}
