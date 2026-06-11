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
    public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
    {
        foreach (var line in fileContent.Split('\n'))
        {
            if (SuppressionCommentParser.MatchesRule(line, ruleName))
            {
                return true;
            }
        }

        if (lineNumber <= 0)
        {
            return false;
        }

        var lines = fileContent.Split('\n');
        if (lineNumber > lines.Length)
        {
            return false;
        }

        return SuppressionCommentParser.MatchesRule(lines[lineNumber - 1], ruleName);
    }
}
