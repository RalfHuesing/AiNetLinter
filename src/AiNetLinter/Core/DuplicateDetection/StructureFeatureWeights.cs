#nullable enable

namespace AiNetLinter.Core.DuplicateDetection;

/// <summary>
/// Feste Gewichte des Structural-Feature-Vektors. Getrennt von den Jaccard-Schwellwerten der
/// Clone-Erkennung, damit Kalibrierung der Typ-4-Suche Lint-Defaults nicht verschiebt.
/// </summary>
internal static class StructureFeatureWeights
{
    internal const double ReturnType = 3.0;
    internal const double ReturnKind = 1.5;
    internal const double ParameterType = 0.8;
    internal const double ParameterKind = 2.0;
    internal const double ControlFlow = 1.2;
    internal const double ControlFlowSequence = 2.0;
    internal const double TargetType = 2.5;
    internal const double MemberType = 1.8;
    internal const double ReturnForm = 2.0;
    internal const double Purity = 2.0;
    internal const double LiteralClass = 0.5;
}
