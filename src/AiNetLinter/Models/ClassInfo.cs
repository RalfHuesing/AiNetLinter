#nullable enable

namespace AiNetLinter.Models;

/// <summary>
/// Hält Informationen über eine gefundene Klasse für Metrikprüfungen.
/// </summary>
public sealed record ClassInfo
{
    /// <summary>
    /// Der Name der Klasse.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der absolute Dateipfad zur Quellcodedatei.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Die Zeilennummer der Klassendeklaration.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Die maximale kognitive Komplexität aller Methoden dieser Klasse.
    /// </summary>
    public required int MaxCognitiveComplexity { get; init; }

    /// <summary>
    /// Die berechnete Vererbungstiefe der Klasse.
    /// </summary>
    public required int InheritanceDepth { get; init; }

    /// <summary>
    /// Der berechnete AI-Context-Footprint der Klasse in Zeilen.
    /// </summary>
    public required int AIContextFootprint { get; init; }

    /// <summary>
    /// Gibt an, ob die Klasse Testmethoden enthält.
    /// </summary>
    public required bool HasTestMethods { get; init; }

    /// <summary>
    /// Gibt an, ob die Klasse als partial deklariert ist.
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Der Name des Projekts, zu dem die Klasse gehört.
    /// </summary>
    public string? ProjectName { get; init; }
}
