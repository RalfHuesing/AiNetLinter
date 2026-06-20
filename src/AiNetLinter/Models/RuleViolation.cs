namespace AiNetLinter.Models;

/// <summary>
/// Repräsentiert einen Regelverstoß im Quellcode.
/// </summary>
public sealed record RuleViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string RuleName { get; init; }
    public required string Details { get; init; }
    public required string Guidance { get; init; }
    /// <summary>
    /// Effektive Severity zur Laufzeit (null = Konfiguration/Registry-Default gilt).
    /// Wird von CompoundSuppression.SeverityOverride gesetzt wenn Bedingungen erfüllt.
    /// </summary>
    public string? EffectiveSeverity { get; init; }
}
