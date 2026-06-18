namespace AiNetLinter.Suppression;

/// <summary>
/// Fügt dateiweite ainetlinter-disable-all-Kommentare in C#-Quelldateien ein.
/// </summary>
public static class DisableAllCommentInjector
{
    /// <summary>
    /// Fügt den Disable-all-Kommentar oben in die angegebenen Dateien ein.
    /// </summary>
    public static DisableAllInjectResult InjectIntoFiles(IReadOnlyList<string> absolutePaths)
    {
        int modified = 0;
        int skipped = 0;

        foreach (var absolutePath in absolutePaths)
        {
            if (TryInjectIntoFile(absolutePath))
            {
                modified++;
            }
            else
            {
                skipped++;
            }
        }

        return new DisableAllInjectResult(absolutePaths.Count, modified, skipped);
    }

    /// <summary>
    /// Fügt den Disable-all-Kommentar in eine einzelne Datei ein, sofern noch nicht vorhanden.
    /// </summary>
    public static bool TryInjectIntoFile(string absolutePath)
    {
        var content = File.ReadAllText(absolutePath);
        if (SuppressionCommentParser.ContainsDisableAll(content))
        {
            return false;
        }

        File.WriteAllText(absolutePath, PrependDisableAll(content));
        return true;
    }

    internal static string PrependDisableAll(string content)
    {
        if (content.StartsWith('\uFEFF'))
        {
            return "\uFEFF" + SuppressionCommentParser.DisableAllLine + Environment.NewLine + content[1..];
        }

        return SuppressionCommentParser.DisableAllLine + Environment.NewLine + content;
    }
}
