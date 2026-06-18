namespace AiNetLinter.Configuration;

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
    /// Exception-Typen, die lautlos abgefangen werden dürfen (Analogon zu AllowCancellationShutdownCatch).
    /// </summary>
    public IReadOnlyList<string>? AllowedSilentCatchExceptionTypes { get; init; }

    /// <summary>
    /// Erzwingt [AsParameters] in Minimal-APIs.
    /// </summary>
    public bool? EnforceMinimalApiAsParameters { get; init; }

    /// <summary>
    /// Erzwingt das Result-Pattern über Exceptions für Kontrollfluss.
    /// </summary>
    public bool? EnforceResultPatternOverExceptions { get; init; }

    public bool? EnforceExplicitStateImmutability { get; init; }
    public IReadOnlyCollection<string>? AllowedExceptions { get; init; }
    public bool? PreventContextDependentOverloads { get; init; }
    public bool? EnforceNamespaceDirectoryMapping { get; init; }
    public string? NamespaceDirectoryMappingMode { get; init; }
    public IReadOnlyCollection<string>? NamespaceDirectoryMappingIgnorePathSegments { get; init; }
    public int? NamespaceDirectoryMappingRequiredTrailingSegments { get; init; }
    public bool? DetectAndBanPhantomDependencies { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptPatterns { get; init; }
    public bool? AllowedEmptyReads { get; init; }
    public IReadOnlyCollection<string>? SealedClassExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptBaseTypes { get; init; }
    public bool? ImmutabilityAllowPrivateBackingFields { get; init; }
    public IReadOnlyCollection<string>? ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
    public bool? ResultPatternAllowCatchRethrow { get; init; }
    public bool? EnablePerformanceProfiling { get; init; }
    public bool? AllowOutParametersInPrivateMethods { get; init; }
    public IReadOnlyCollection<string>? SemanticNamingExemptMethodNames { get; init; }
    public bool? SemanticNamingAllowSubstringOfMethodName { get; init; }

    /// <summary>
    /// Verbietet public/internal nested Typen (Override fuer Global.BanPublicNestedTypes).
    /// </summary>
    public bool? BanPublicNestedTypes { get; init; }

    /// <summary>
    /// Erlaubt private nested Typen weiterhin (Override fuer Global.BanPublicNestedTypesAllowPrivate).
    /// </summary>
    public bool? BanPublicNestedTypesAllowPrivate { get; init; }
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
    /// Maximale Parameteranzahl pro Methode in Testdateien. 0 = gleicher Grenzwert wie MaxMethodParameterCount.
    /// </summary>
    public int? MaxMethodParameterCountInTestFiles { get; init; }

    /// <summary>
    /// Typ-Namen, die beim Zählen der Parameter-Anzahl ignoriert werden (z. B. "CancellationToken").
    /// </summary>
    public IReadOnlyCollection<string>? MethodParameterCountIgnoreTypeNames { get; init; }

    /// <summary>
    /// Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden (z. B. "ILogger" deckt ILogger<T> ab).
    /// </summary>
    public IReadOnlyCollection<string>? MethodParameterCountIgnoreTypePrefixes { get; init; }

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

    public int? MaxDirectoryDepth { get; init; }

    /// <summary>
    /// Namespace-Präfixe von Framework-Basistypen, die beim Zählen der Vererbungstiefe ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string>? InheritanceDepthFrameworkPrefixes { get; init; }

    /// <summary>
    /// Typ-Name-Präfixe von Framework-/Cross-Cutting-Abhängigkeiten, die nicht
    /// zu MaxConstructorDependencies zählen.
    /// </summary>
    public IReadOnlyCollection<string>? ConstructorDependencyIgnoreTypePrefixes { get; init; }

    /// <summary>
    /// Klassen-Name-Suffixe, für die MaxConstructorDependencies nicht geprüft wird.
    /// </summary>
    public IReadOnlyCollection<string>? ConstructorDependencyExemptClassSuffixes { get; init; }

    public int? ComplexityNearMissTolerance { get; init; }
    public bool? ExcludeSwitchDispatcherCases { get; init; }
    public int? SwitchDispatcherMaxCaseBodyLines { get; init; }

    /// <summary>
    /// Namespace-Präfixe von Typen, die beim Footprint-Check ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string>? FootprintIgnoreNamespacePrefixes { get; init; }

    /// <summary>
    /// Einfache Typ-Namen (kein Namespace), die beim AIContextFootprint nicht mitgezählt werden.
    /// </summary>
    public IReadOnlyCollection<string>? FootprintIgnoreTypeNames { get; init; }

    public int? MaxBoolParameterCount { get; init; }
    public bool? MaxBoolParameterCountAllowPrivate { get; init; }
    public IReadOnlyCollection<string>? MaxBoolParameterCountExemptMethodPrefixes { get; init; }

    public int? MaxDirectoryChildren { get; init; }
    public IReadOnlyCollection<string>? MaxDirectoryChildrenExemptNames { get; init; }

    public int? MaxPartialClassFiles { get; init; }
    public IReadOnlyCollection<string>? MaxPartialClassFilesExemptTypes { get; init; }

    public int? MaxPublicMembersPerType { get; init; }
    public IReadOnlyCollection<string>? MaxPublicMembersPerTypeExemptSuffixes { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die TestSentinel-Konfiguration (pro Projekt).
/// </summary>
public sealed record TestSentinelConfigOverride
{
    /// <summary>
    /// Klassen-Name-Suffixe, die vom StaticTestSentinel ausgenommen werden.
    /// </summary>
    public IReadOnlyCollection<string>? ExemptClassNameSuffixes { get; init; }

    /// <summary>
    /// Basistypen oder Interfaces: Klassen die davon erben/implementieren werden ausgenommen.
    /// </summary>
    public IReadOnlyCollection<string>? ExemptWhenInheritsFrom { get; init; }

    /// <summary>
    /// Statische Klassen ausgenommen wenn true.
    /// </summary>
    public bool? ExemptStaticClasses { get; init; }

    /// <summary>
    /// Projekt-Name-Suffixe, die ein Projekt als Testprojekt markieren.
    /// </summary>
    public IReadOnlyList<string>? TestProjectNameSuffixes { get; init; }
}

