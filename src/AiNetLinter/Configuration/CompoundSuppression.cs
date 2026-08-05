#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Eine Bedingung über eine einzelne Metrik.
/// Wird in <see cref="CompoundSuppression.WhenAllOf"/> verwendet.
/// </summary>
public sealed record MetricCondition
{
    /// <summary>
    /// Name der Metrik. Gültige Werte: "CyclomaticComplexity", "CognitiveComplexity",
    /// "ParameterCount", "LineCount" (Methoden); "ConstructorDependencies", "PublicMemberCount" (Klassen).
    /// Unbekannte Namen deaktivieren die Bedingung ohne Absturz.
    /// </summary>
    public required string Metric { get; init; }

    /// <summary>Bedingung: Metrikwert ≤ AtMost.</summary>
    public int? AtMost { get; init; }

    /// <summary>Bedingung: Metrikwert ≥ AtLeast. Für Eskalations-Szenarien.</summary>
    public int? AtLeast { get; init; }
}

/// <summary>
/// Unterdrückt eine Regel kontextabhängig, wenn koinzidente Metriken niedrig sind.
/// Reduziert False Positives ohne die eigentlichen AI-Readability-Ziele zu kompromittieren.
/// </summary>
public sealed record CompoundSuppression
{
    /// <summary>
    /// Die Rule-ID, die supprimiert werden soll (z. B. "MaxMethodLineCount").
    /// Muss einer bekannten Rule-ID in <see cref="LinterRuleIds"/> entsprechen.
    /// </summary>
    public required string TargetRule { get; init; }

    /// <summary>
    /// Alle Bedingungen müssen erfüllt sein (AND-Verknüpfung) damit die Suppression aktiv wird.
    /// </summary>
    public required IReadOnlyList<MetricCondition> WhenAllOf { get; init; }

    /// <summary>
    /// Wenn gesetzt: Statt des konfigurierten Limits gilt dieser Wert.
    /// Wenn null: Violation wird vollständig unterdrückt.
    /// </summary>
    public int? RelaxedLimit { get; init; }

    /// <summary>
    /// Optionale Severity-Herabstufung wenn Bedingungen erfüllt aber RelaxedLimit überschritten.
    /// Erlaubte Werte: "warning", "error". Wirkt nur in Kombination mit RelaxedLimit.
    /// </summary>
    public string? SeverityOverride { get; init; }

    /// <summary>
    /// Optionaler Freitext-Grund. Wird in .mdc-Output und Violation-Guidance wiedergegeben.
    /// </summary>
    public string? Reason { get; init; }
}
