namespace AiNetLinter.Suppression;

/// <summary>
/// Parst ainetlinter-disable-Kommentare in Quellcodezeilen.
/// </summary>
public static class SuppressionCommentParser
{
    /// <summary>
    /// Marker-Text für Suppression-Kommentare.
    /// </summary>
    public const string DisableMarker = "ainetlinter-disable";

    /// <summary>
    /// Standardzeile zum dateiweiten Deaktivieren aller Regeln.
    /// </summary>
    public const string DisableAllLine = "// ainetlinter-disable all";

    /// <summary>
    /// Prüft, ob eine Zeile die angegebene Regel unterdrückt.
    /// </summary>
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

    /// <summary>
    /// Prüft, ob der Dateiinhalt bereits einen dateiweiten Disable-all-Kommentar enthält.
    /// </summary>
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

    /// <summary>
    /// Prüft, ob eine Zeile alle Regeln unterdrückt.
    /// </summary>
    public static bool MatchesDisableAll(string lineText)
    {
        var suffix = GetDisableSuffix(lineText);
        return suffix != null && IsDisableAllSuffix(suffix);
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
