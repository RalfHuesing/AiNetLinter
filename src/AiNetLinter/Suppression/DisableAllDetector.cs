using System.IO;
using System.Text.RegularExpressions;

namespace AiNetLinter.Suppression;

public static partial class DisableAllDetector
{
    public static bool HasDisableAll(string fileContent, IgnoreSuppressionsFilter? filter = null, string languageKind = "cs")
    {
        if (filter != null && filter.ShouldIgnoreSuppression(languageKind))
        {
            return false;
        }

        return DisableAllLinePattern().IsMatch(fileContent);
    }

    public static bool FileHasDisableAll(string absolutePath, IgnoreSuppressionsFilter? filter = null)
    {
        if (!File.Exists(absolutePath))
        {
            return false;
        }

        if (filter != null && filter.ShouldIgnoreSuppressionForFile(absolutePath))
        {
            return false;
        }

        return HasDisableAll(File.ReadAllText(absolutePath));
    }

    [GeneratedRegex(@"^// ainetlinter-disable all(?:\r?\n|$)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    internal static partial Regex DisableAllLinePattern();
}
