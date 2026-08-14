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

    /// <summary>
    /// Prüft, ob ein relativer Dateipfad eine Testdatei darstellt.
    /// </summary>
    public static bool IsTestFile(string relativePath)
    {
        var normalized = NormalizeSeparators(relativePath);
        return normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".FastTests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".IntegrationTests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".TestKit/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".Specs/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalisiert Pfad-Trennzeichen auf einheitliche Forward-Slashes ('/'),
    /// damit String- und Scope-Vergleiche plattformunabhängig funktionieren.
    /// </summary>
    public static string NormalizeSeparators(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Prüft, ob ein Dateipfad (relativ oder absolut) in einen Scope-Filter fällt.
    /// Beide Pfade werden vor dem Vergleich auf einheitliche Forward-Slashes normalisiert.
    /// </summary>
    public static bool MatchesScope(string? filePath, string? scopeFilter)
    {
        if (string.IsNullOrWhiteSpace(scopeFilter)) return true;
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        var normalizedPath = NormalizeSeparators(filePath);
        var normalizedFilter = NormalizeSeparators(scopeFilter);

        return normalizedPath.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }
}
