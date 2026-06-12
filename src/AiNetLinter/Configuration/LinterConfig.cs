namespace AiNetLinter.Configuration;

/// <summary>
/// Die globale Konfigurationsstruktur für den Linter.
/// </summary>
public sealed record LinterConfig
{
    public required GlobalConfig Global { get; init; }
    public required MetricsConfig Metrics { get; init; }
    public TestSentinelConfig TestSentinel { get; init; } = new();
    public IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; init; }
        = new Dictionary<string, RuleMetadataEntry>();
    public IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; init; } = Array.Empty<NamespaceRule>();
}

/// <summary>
/// Definition einer verbotenen Abhängigkeit zwischen Namespaces.
/// </summary>
public sealed record NamespaceRule
{
    public required string SourceNamespace { get; init; }
    public required string TargetNamespace { get; init; }
}

/// <summary>
/// Globale Verhaltensregeln und strukturelle Einschränkungen.
/// </summary>
public sealed record GlobalConfig
{
    public bool EnforceSealedClasses { get; init; } = true;
    public bool AllowUnsealedPartialClasses { get; init; } = false;
    public bool AllowDynamic { get; init; } = false;
    public bool AllowOutParameters { get; init; } = false;
    public bool EnforceValueObjectContracts { get; init; } = true;
    public bool EnableTestSentinel { get; init; } = true;
    public bool EnforcePascalCase { get; init; } = true;
    public bool EnforceXmlDocumentation { get; init; } = true;
    public bool EnforceSemanticNaming { get; init; } = true;
    public bool EnforceNullableEnable { get; init; } = true;
    public bool EnforceNoSilentCatch { get; init; } = true;
    public bool AllowTryPatternOutParameters { get; init; } = true;
    public bool AllowCancellationShutdownCatch { get; init; } = true;
    public bool EnforceMinimalApiAsParameters { get; init; } = false;
}

/// <summary>
/// Grenzwerte für verschiedene Code-Metriken.
/// </summary>
public sealed record MetricsConfig
{
    public int MaxLineCount { get; init; } = 500;
    public int MaxMethodParameterCount { get; init; } = 4;
    public int MaxMethodLineCount { get; init; } = 42;
    public int MaxCyclomaticComplexity { get; init; } = 5;
    public int MaxCognitiveComplexity { get; init; } = 5;
    public int MaxInheritanceDepth { get; init; } = 2;
    public int MinCognitiveComplexityForTest { get; init; } = 3;
    public bool AggregatePartialClassLineCount { get; init; } = false;
}

/// <summary>
/// Konfiguration für den Static Test Sentinel (flexible Testabdeckungserkennung).
/// </summary>
public sealed record TestSentinelConfig
{
    public IReadOnlyList<string> ClassNamePatterns { get; init; } =
    [
        "{Name}Tests",
        "{Name}Test",
        "{Name}IntegrationTests",
        "{Name}*Tests",
    ];

    public bool RecognizeTypeofReference { get; init; } = true;
    public bool RecognizeCoversComment { get; init; } = true;
}

/// <summary>
/// Agent-Metadaten pro Regel (Severity und Intent für LLM-Priorisierung).
/// </summary>
public sealed record RuleMetadataEntry
{
    public string Severity { get; init; } = "error";
    public string Intent { get; init; } = "general";
}
