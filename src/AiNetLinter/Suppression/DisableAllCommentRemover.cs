using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AiNetLinter.Suppression;

/// <summary>
/// Entfernt exakte ainetlinter-disable-all-Zeilen aus C#-Quelldateien.
/// </summary>
public static class DisableAllCommentRemover
{
    /// <summary>
    /// Entfernt exakte Disable-all-Zeilen aus allen analysierbaren .cs-Dateien unter path.
    /// </summary>
    public static async Task<DisableAllRemoveResult> RemoveAsync(string path)
    {
        var absolutePaths = await SuppressionFileResolver.ResolveAbsolutePathsAsync(path);
        return RemoveFromFiles(absolutePaths);
    }

    /// <summary>
    /// Entfernt exakte Disable-all-Zeilen aus einer Datei, sofern vorhanden.
    /// </summary>
    public static bool TryRemoveFromFile(string absolutePath)
    {
        var content = File.ReadAllText(absolutePath);
        var updated = RemoveDisableAllLines(content);
        if (string.Equals(content, updated, StringComparison.Ordinal))
        {
            return false;
        }

        File.WriteAllText(absolutePath, updated);
        return true;
    }

    internal static string RemoveDisableAllLines(string content)
    {
        return DisableAllDetector.DisableAllLinePattern().Replace(content, string.Empty);
    }

    private static DisableAllRemoveResult RemoveFromFiles(IReadOnlyList<string> absolutePaths)
    {
        int modified = 0;

        foreach (var absolutePath in absolutePaths)
        {
            if (TryRemoveFromFile(absolutePath))
            {
                modified++;
            }
        }

        return new DisableAllRemoveResult(absolutePaths.Count, modified);
    }
}
