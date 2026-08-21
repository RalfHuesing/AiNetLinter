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
    Structural,
}

/// <summary>Wire-Label der Modi (Request-Parameter, Summary-Feld und Text-Report nutzen
/// dieselben Strings) — zentrale Konstanten statt verteilter Literale.</summary>
internal static class DuplicateDetectionModeLabels
{
    internal const string Clone = "clone";
    internal const string Structural = "structural";
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
            DuplicateDetectionModeLabels.Clone => DuplicateDetectionMode.Clone,
            "refactoring-drift" => DuplicateDetectionMode.RefactoringDrift,
            DuplicateDetectionModeLabels.Structural => DuplicateDetectionMode.Structural,
            _ => null,
        };
    }
}
