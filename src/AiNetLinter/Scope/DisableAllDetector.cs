using System.Text.RegularExpressions;

namespace AiNetLinter.Scope;

/// <summary>
/// Erkennt Dateien mit dateiweitem ainetlinter-disable-all-Kommentar.
/// </summary>
public static partial class DisableAllDetector
{
    /// <summary>
    /// Prüft, ob der Dateiinhalt einen exakten Disable-all-Kommentar enthält.
    /// </summary>
    public static bool HasDisableAll(string fileContent) =>
        DisableAllLinePattern().IsMatch(fileContent);

    /// <summary>
    /// Prüft, ob die Datei unter dem angegebenen Pfad Disable-all enthält.
    /// </summary>
    public static bool FileHasDisableAll(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return false;
        }

        return HasDisableAll(File.ReadAllText(absolutePath));
    }

    [GeneratedRegex(@"^// ainetlinter-disable all(?:\r?\n|$)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DisableAllLinePattern();
}
