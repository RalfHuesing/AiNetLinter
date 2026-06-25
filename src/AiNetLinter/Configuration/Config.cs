#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Die globale Konfigurationsstruktur für den Linter.
/// </summary>
public sealed record LinterConfig
{
    public required GlobalConfig Global { get; init; }
    public required MetricsConfig Metrics { get; init; }
    public TestSentinelConfig TestSentinel { get; init; } = new();
    public UiSeparationConfig UiSeparation { get; init; } = new();
    public FileFiltersConfig FileFilters { get; init; } = new();
    public WebConfig Web { get; init; } = new();
    public IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; init; }
        = new Dictionary<string, RuleMetadataEntry>();
    public IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; init; } = Array.Empty<NamespaceRule>();

    /// <summary>
    /// Projekt-spezifische Konfigurations-Überschreibungen basierend auf Glob-Mustern.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; init; }
        = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pfadbasierte Konfigurations-Überschreibungen. Der Key ist ein Glob-Muster
    /// (z. B. "src/MyApp/Handlers/**") gegen den relativen Dateipfad ab Solution-Root.
    /// Wird NACH ProjectOverrides angewendet; gewinnt bei Konflikt.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; init; }
        = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Basis-Pfad der Solution (für relative Pfadberechnung bei PathOverrides).
    /// Wird vom LinterEngine beim Laden gesetzt.
    /// </summary>
    public string? SolutionBasePath { get; init; }
}

/// <summary>
/// Definition einer verbotenen Abhängigkeit zwischen Namespaces.
/// </summary>
public sealed record NamespaceRule
{
    public required string SourceNamespace { get; init; }
    public required string TargetNamespace { get; init; }
}
