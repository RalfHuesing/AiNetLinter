#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Agent-Metadaten pro Regel (Severity und Intent für LLM-Priorisierung).
/// </summary>
public sealed record RuleMetadataEntry
{
    public string Severity { get; init; } = "error";
    public string Intent { get; init; } = "general";
}

/// <summary>
/// Definiert überschreibbare Abschnitte für ein Projekt.
/// </summary>
public sealed record ProjectOverrideEntry
{
    /// <summary>
    /// Überschreibungen der globalen Konfigurationswerte.
    /// </summary>
    public GlobalConfigOverride? Global { get; init; }

    /// <summary>
    /// Überschreibungen der Metrik-Grenzwerte.
    /// </summary>
    public MetricsConfigOverride? Metrics { get; init; }

    /// <summary>
    /// Überschreibungen der TestSentinel-Konfiguration (Ausnahmen vom Testpflicht-Check).
    /// </summary>
    public TestSentinelConfigOverride? TestSentinel { get; init; }

    /// <summary>
    /// Überschreibungen der UI-Trennungsregeln (Blazor/WPF).
    /// </summary>
    public UiSeparationConfigOverride? UiSeparation { get; init; }
}
