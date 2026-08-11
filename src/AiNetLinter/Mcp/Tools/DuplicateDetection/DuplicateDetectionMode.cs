#nullable enable

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Betriebsmodi von <c>find_duplicates</c> (Muster analog <c>MetricsTreeMode</c>/
/// <c>MetricsTreeModeParser</c>). <see cref="Clone"/> = solution-weite Cluster-Suche,
/// <see cref="RefactoringDrift"/> = Kandidaten-Suche relativ zu einem einzelnen Helper-Symbol.
/// </summary>
internal enum DuplicateDetectionMode
{
    Clone,
    RefactoringDrift,
}

/// <summary>Parst den <c>mode</c>-Parameter von <c>find_duplicates</c>. Leer/<see langword="null"/>
/// = Default <see cref="DuplicateDetectionMode.Clone"/> (Aufrufe ohne <c>mode</c>-Argument
/// verhalten sich unveraendert).</summary>
internal static class DuplicateDetectionModeParser
{
    internal static DuplicateDetectionMode? TryParse(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return DuplicateDetectionMode.Clone;
        return mode.Trim().ToLowerInvariant() switch
        {
            "clone" => DuplicateDetectionMode.Clone,
            "refactoring-drift" => DuplicateDetectionMode.RefactoringDrift,
            _ => null,
        };
    }
}
