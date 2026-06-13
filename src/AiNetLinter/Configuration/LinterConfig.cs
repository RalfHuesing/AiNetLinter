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

    /// <summary>
    /// Projekt-spezifische Konfigurations-Überschreibungen basierend auf Glob-Mustern.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; init; }
        = new Dictionary<string, ProjectOverrideEntry>(StringComparer.OrdinalIgnoreCase);
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
    public bool EnforceResultPatternOverExceptions { get; init; } = true;
    public bool EnforceNoVariableShadowing { get; init; } = true;
    public bool EnforceReadonlyParameters { get; init; } = true;
    public bool EnforceReadonlyFields { get; init; } = true;
    public bool EnforceNoMagicValues { get; init; } = true;
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
    public int MaxMethodOverloads { get; init; } = 2;
    public int MaxConstructorDependencies { get; init; } = 5;

    /// <summary>
    /// Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten.
    /// </summary>
    public int MaxAIContextFootprint { get; init; } = 5000;
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
}

/// <summary>
/// Optionale Überschreibungen für globale Linter-Regeln.
/// </summary>
public sealed record GlobalConfigOverride
{
    /// <summary>
    /// Erzwingt, dass konkrete Klassen als sealed deklariert sein müssen.
    /// </summary>
    public bool? EnforceSealedClasses { get; init; }

    /// <summary>
    /// Erlaubt unversiegelte partial Klassen.
    /// </summary>
    public bool? AllowUnsealedPartialClasses { get; init; }

    /// <summary>
    /// Erlaubt die Verwendung von dynamic.
    /// </summary>
    public bool? AllowDynamic { get; init; }

    /// <summary>
    /// Erlaubt out Parameter.
    /// </summary>
    public bool? AllowOutParameters { get; init; }

    /// <summary>
    /// Erzwingt Value Object Verträge (record/readonly struct mit readonly Eigenschaften).
    /// </summary>
    public bool? EnforceValueObjectContracts { get; init; }

    /// <summary>
    /// Aktiviert den Static Test Sentinel.
    /// </summary>
    public bool? EnableTestSentinel { get; init; }

    /// <summary>
    /// Erzwingt PascalCase für Identifier.
    /// </summary>
    public bool? EnforcePascalCase { get; init; }

    /// <summary>
    /// Erzwingt XML-Dokumentation für öffentliche Member.
    /// </summary>
    public bool? EnforceXmlDocumentation { get; init; }

    /// <summary>
    /// Erzwingt semantische Parameternamen.
    /// </summary>
    public bool? EnforceSemanticNaming { get; init; }

    /// <summary>
    /// Erzwingt nullable enable.
    /// </summary>
    public bool? EnforceNullableEnable { get; init; }

    /// <summary>
    /// Erzwingt, dass keine Exceptions stumm abgefangen werden.
    /// </summary>
    public bool? EnforceNoSilentCatch { get; init; }

    /// <summary>
    /// Erlaubt out Parameter in Try*-Methoden.
    /// </summary>
    public bool? AllowTryPatternOutParameters { get; init; }

    /// <summary>
    /// Erlaubt das Abfangen von OperationCanceledException beim Shutdown.
    /// </summary>
    public bool? AllowCancellationShutdownCatch { get; init; }

    /// <summary>
    /// Erzwingt [AsParameters] in Minimal-APIs.
    /// </summary>
    public bool? EnforceMinimalApiAsParameters { get; init; }

    /// <summary>
    /// Erzwingt das Result-Pattern über Exceptions für Kontrollfluss.
    /// </summary>
    public bool? EnforceResultPatternOverExceptions { get; init; }

    /// <summary>
    /// Verbietet Shadowing von Variablen/Parametern.
    /// </summary>
    public bool? EnforceNoVariableShadowing { get; init; }

    /// <summary>
    /// Verbietet Zuweisungen an Parameter.
    /// </summary>
    public bool? EnforceReadonlyParameters { get; init; }

    /// <summary>
    /// Erzwingt readonly private Felder, falls nur im Ctor zugewiesen.
    /// </summary>
    public bool? EnforceReadonlyFields { get; init; }

    /// <summary>
    /// Verbietet magische Literale.
    /// </summary>
    public bool? EnforceNoMagicValues { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für Linter-Metrik-Grenzwerte.
/// </summary>
public sealed record MetricsConfigOverride
{
    /// <summary>
    /// Maximale Zeilenanzahl pro Datei.
    /// </summary>
    public int? MaxLineCount { get; init; }

    /// <summary>
    /// Maximale Parameteranzahl pro Methode.
    /// </summary>
    public int? MaxMethodParameterCount { get; init; }

    /// <summary>
    /// Maximale Zeilenanzahl pro Methode.
    /// </summary>
    public int? MaxMethodLineCount { get; init; }

    /// <summary>
    /// Maximale zyklomatische Komplexität pro Methode.
    /// </summary>
    public int? MaxCyclomaticComplexity { get; init; }

    /// <summary>
    /// Maximale kognitive Komplexität pro Methode.
    /// </summary>
    public int? MaxCognitiveComplexity { get; init; }

    /// <summary>
    /// Maximale Vererbungstiefe.
    /// </summary>
    public int? MaxInheritanceDepth { get; init; }

    /// <summary>
    /// Minimale kognitive Komplexität einer Methode, um Tests vorauszusetzen.
    /// </summary>
    public int? MinCognitiveComplexityForTest { get; init; }

    /// <summary>
    /// Aggregiert Zeilenanzahl über partial Klassen.
    /// </summary>
    public bool? AggregatePartialClassLineCount { get; init; }

    /// <summary>
    /// Maximale Anzahl an Methodenüberladungen.
    /// </summary>
    public int? MaxMethodOverloads { get; init; }

    /// <summary>
    /// Maximale Anzahl von Konstruktor-Abhängigkeiten.
    /// </summary>
    public int? MaxConstructorDependencies { get; init; }

    /// <summary>
    /// Der maximale transitive AI-Context-Footprint.
    /// </summary>
    public int? MaxAIContextFootprint { get; init; }
}
