#nullable enable

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Modi des <c>metrics_tree</c>-Tools. Enthaelt bewusst nur die zwei in EPIC-01 implementierten
/// Datei-Walk-Modi — kein Platzhalter fuer die Roslyn-basierten <c>violation_density</c>/
/// <c>complexity</c>-Modi aus EPIC-02, die dieses Enum dann erweitern.
/// </summary>
internal enum MetricsTreeMode
{
    CodeSize,
    CommentDensity,
}

/// <summary>Parst den <c>mode</c>-Parameter von <c>metrics_tree</c> in ein <see cref="MetricsTreeMode"/>.</summary>
internal static class MetricsTreeModeParser
{
    internal static MetricsTreeMode? TryParse(string mode) => mode switch
    {
        "code_size" => MetricsTreeMode.CodeSize,
        "comment_density" => MetricsTreeMode.CommentDensity,
        _ => null,
    };
}
