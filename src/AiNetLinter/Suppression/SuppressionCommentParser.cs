namespace AiNetLinter.Suppression;

public static class SuppressionCommentParser
{
    public const string DisableMarker = "ainetlinter-disable";

    public const string DisableAllLine = "// ainetlinter-disable all";

    public static bool MatchesRule(string lineText, string ruleName)
    {
        var suffix = GetDisableSuffix(lineText);
        if (suffix == null)
        {
            return false;
        }

        return IsDisableAllSuffix(suffix) ||
               suffix.StartsWith(ruleName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsDisableAll(string fileContent)
    {
        foreach (var line in fileContent.Split('\n'))
        {
            if (MatchesDisableAll(line))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesDisableAll(string lineText)
    {
        var suffix = GetDisableSuffix(lineText);
        return suffix != null && IsDisableAllSuffix(suffix);
    }

    public static bool IsExactDisableAllLine(string lineText)
    {
        var normalizedLine = lineText.TrimEnd('\r');
        return string.Equals(normalizedLine, DisableAllLine, StringComparison.Ordinal);
    }

    private static string? GetDisableSuffix(string lineText)
    {
        int index = lineText.IndexOf(DisableMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        return lineText.Substring(index + DisableMarker.Length).Trim();
    }

    private static bool IsDisableAllSuffix(string suffix)
    {
        return suffix.Length == 0 ||
               suffix.Equals("all", StringComparison.OrdinalIgnoreCase);
    }
}
