#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Lese-Sicht auf die Linter-Konfiguration, die der Linter und seine Konsumenten tatsaechlich
/// benoetigen. Wird von <see cref="Config"/> implementiert und ermoeglicht es Aufrufern
/// (z. B. <c>McpCodeGraphServer</c>), die Config-Eigenschaft schmal zu exposen, ohne den
/// vollstaendigen <c>Configuration</c>-Namespace in ihren AIContextFootprint zu ziehen.
/// </summary>
internal interface ILinterEngineConfig
{
    GlobalConfig Global { get; }
    MetricsConfig Metrics { get; }
    TestSentinelConfig TestSentinel { get; }
    FileFiltersConfig FileFilters { get; }
    UiSeparationConfig UiSeparation { get; }
    WebConfig Web { get; }
    IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; }
    IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; }
    IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; }
    IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; }
    string? SolutionBasePath { get; }
}
