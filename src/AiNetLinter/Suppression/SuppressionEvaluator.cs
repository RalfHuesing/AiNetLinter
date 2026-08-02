#nullable enable

namespace AiNetLinter.Suppression;

public static class SuppressionEvaluator
{
    public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber, System.Collections.Generic.IReadOnlyList<string>? ignoreSuppressions = null)
    {
        if (ignoreSuppressions != null && ignoreSuppressions.Count > 0)
        {
            var filter = new IgnoreSuppressionsFilter(ignoreSuppressions);
            if (filter.ShouldIgnoreSuppression("cs"))
            {
                return false;
            }
        }

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

    public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber, IgnoreSuppressionsFilter? ignoreFilter) =>
        IsSuppressed(fileContent, ruleName, lineNumber, ignoreFilter?.ActiveLanguages);
}
