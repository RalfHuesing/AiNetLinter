#nullable enable

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Betriebsmodi von <c>find_duplicates</c> (Muster analog <c>MetricsTreeMode</c>/
/// <c>MetricsTreeModeParser</c>). <see cref="Clone"/> ist Teil A (solution-weite Cluster-Suche),
/// <see cref="RefactoringDrift"/> ist Teil C (Kandidaten-Suche relativ zu einem einzelnen
/// Helper-Symbol, siehe <c>tasks/features/07-drift-audit-ideen.md</c> §C).
/// </summary>
internal enum DuplicateDetectionMode
{
    Clone,
    RefactoringDrift,
}

/// <summary>Parst den <c>mode</c>-Parameter von <c>find_duplicates</c>. Leer/<see langword="null"/>
/// = Default <see cref="DuplicateDetectionMode.Clone"/> (bestehendes Teil-A-Verhalten, damit
/// bestehende Aufrufe ohne <c>mode</c>-Argument unveraendert funktionieren).</summary>
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
