namespace AiNetLinter.Output;

/// <summary>
/// Normalisiert absolute Dateipfade zu relativen Pfaden für die CLI-Ausgabe.
/// </summary>
public static class PathNormalizer
{
    /// <summary>
    /// Wandelt einen absoluten Dateipfad in einen relativen Pfad zur Output-Basis um.
    /// Verwendet Forward-Slashes für plattformunabhängige LLM-Lesbarkeit.
    /// </summary>
    public static string ToRelative(string outputRoot, string absoluteFilePath)
    {
        if (string.IsNullOrEmpty(absoluteFilePath))
        {
            return string.Empty;
        }

        var normalizedRoot = Path.GetFullPath(outputRoot);
        var normalizedFile = Path.GetFullPath(absoluteFilePath);

        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalizedFile);
        }

        var relative = Path.GetRelativePath(normalizedRoot, normalizedFile);
        return relative.Replace('\\', '/');
    }
}
