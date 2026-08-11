#nullable enable

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>
/// Modi des <c>metrics_tree</c>-Tools. Die zwei Datei-Walk-Modi (<c>code_size</c>,
/// <c>comment_density</c>) laufen synchron ueber <see cref="MetricsTreeScanner"/> ohne
/// <see cref="Microsoft.CodeAnalysis.Solution"/>-Overhead; die zwei Roslyn-Modi
/// (<c>violation_density</c>, <c>complexity</c>) laufen ueber
/// <see cref="MetricsTreeRoslynScanner"/> und nutzen <c>LinterEngine</c> bzw. Syntax-Baeume.
/// </summary>
internal enum MetricsTreeMode
{
    CodeSize,
    CommentDensity,
    ViolationDensity,
    Complexity,
}

/// <summary>Parst den <c>mode</c>-Parameter von <c>metrics_tree</c> in ein <see cref="MetricsTreeMode"/>.</summary>
internal static class MetricsTreeModeParser
{
    internal static MetricsTreeMode? TryParse(string mode) => mode switch
    {
        "code_size" => MetricsTreeMode.CodeSize,
        "comment_density" => MetricsTreeMode.CommentDensity,
        "violation_density" => MetricsTreeMode.ViolationDensity,
        "complexity" => MetricsTreeMode.Complexity,
        _ => null,
    };
}
