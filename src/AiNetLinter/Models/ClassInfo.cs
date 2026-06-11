using Microsoft.CodeAnalysis;

namespace AiNetLinter.Models;

/// <summary>
/// Hält Informationen über eine gefundene Klasse für Metrikprüfungen.
/// </summary>
public sealed record ClassInfo
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required int MaxCognitiveComplexity { get; init; }
    public required INamedTypeSymbol Symbol { get; init; }
    public required bool HasTestMethods { get; init; }
    public bool IsPartial { get; init; }
}
