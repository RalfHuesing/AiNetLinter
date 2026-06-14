namespace AiNetLinter.Configuration;

/// <summary>
/// Die globale Konfigurationsstruktur für den Linter.
/// </summary>
public sealed record LinterConfig
{
    public required GlobalConfig Global { get; init; }
    public required MetricsConfig Metrics { get; init; }
    public TestSentinelConfig TestSentinel { get; init; } = new();
    public MagicValuesConfig MagicValues { get; init; } = new();
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
    public bool EnforceExplicitStateImmutability { get; init; } = true;
    public IReadOnlyCollection<string> AllowedExceptions { get; init; } = new[]
    {
        "ArgumentException",
        "ArgumentNullException",
        "ArgumentOutOfRangeException",
        "InvalidOperationException",
        "NotSupportedException",
        "KeyNotFoundException",
        "IndexOutOfRangeException",
        "TimeoutException",
        "ObjectDisposedException",
        "NotImplementedException"
    };
    public bool EnforceStrictBoundaryForBusinessLogic { get; init; } = true;
    public bool PreventContextDependentOverloads { get; init; } = true;
    public bool RequireExplicitTruncationHandling { get; init; } = true;
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;
    public bool DetectAndBanPhantomDependencies { get; init; } = true;
    public IReadOnlyCollection<string> ImmutabilityExemptSuffixes { get; init; } = new[]
    {
        "Dto", "Entity", "Model", "Request", "Response", "Command"
    };
    public IReadOnlyCollection<string> ImmutabilityExemptPatterns { get; init; } = Array.Empty<string>();
    public bool AllowedEmptyReads { get; init; } = false;
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; } = Array.Empty<string>();
    public bool ImmutabilityAllowPrivateBackingFields { get; init; } = false;

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public GlobalConfig Apply(GlobalConfigOverride? @override)
    {
        if (@override == null) return this;
        var result = ApplyCore1(@override);
        return result.ApplyCore2(@override);
    }

    private GlobalConfig ApplyCore1(GlobalConfigOverride @override)
    {
        return this with
        {
            EnforceSealedClasses = @override.EnforceSealedClasses ?? EnforceSealedClasses,
            AllowUnsealedPartialClasses = @override.AllowUnsealedPartialClasses ?? AllowUnsealedPartialClasses,
            AllowDynamic = @override.AllowDynamic ?? AllowDynamic,
            AllowOutParameters = @override.AllowOutParameters ?? AllowOutParameters,
            EnforceValueObjectContracts = @override.EnforceValueObjectContracts ?? EnforceValueObjectContracts,
            EnableTestSentinel = @override.EnableTestSentinel ?? EnableTestSentinel,
            EnforcePascalCase = @override.EnforcePascalCase ?? EnforcePascalCase,
            EnforceXmlDocumentation = @override.EnforceXmlDocumentation ?? EnforceXmlDocumentation,
            EnforceSemanticNaming = @override.EnforceSemanticNaming ?? EnforceSemanticNaming,
            EnforceNullableEnable = @override.EnforceNullableEnable ?? EnforceNullableEnable,
            EnforceNoSilentCatch = @override.EnforceNoSilentCatch ?? EnforceNoSilentCatch,
            AllowTryPatternOutParameters = @override.AllowTryPatternOutParameters ?? AllowTryPatternOutParameters,
            AllowCancellationShutdownCatch = @override.AllowCancellationShutdownCatch ?? AllowCancellationShutdownCatch,
            EnforceMinimalApiAsParameters = @override.EnforceMinimalApiAsParameters ?? EnforceMinimalApiAsParameters,
            EnforceResultPatternOverExceptions = @override.EnforceResultPatternOverExceptions ?? EnforceResultPatternOverExceptions,
        };
    }

    private GlobalConfig ApplyCore2(GlobalConfigOverride @override)
    {
        return this with
        {
            EnforceNoVariableShadowing = @override.EnforceNoVariableShadowing ?? EnforceNoVariableShadowing,
            EnforceReadonlyParameters = @override.EnforceReadonlyParameters ?? EnforceReadonlyParameters,
            EnforceReadonlyFields = @override.EnforceReadonlyFields ?? EnforceReadonlyFields,
            EnforceNoMagicValues = @override.EnforceNoMagicValues ?? EnforceNoMagicValues,
            EnforceExplicitStateImmutability = @override.EnforceExplicitStateImmutability ?? EnforceExplicitStateImmutability,
            AllowedExceptions = @override.AllowedExceptions ?? AllowedExceptions,
            EnforceStrictBoundaryForBusinessLogic = @override.EnforceStrictBoundaryForBusinessLogic ?? EnforceStrictBoundaryForBusinessLogic,
            PreventContextDependentOverloads = @override.PreventContextDependentOverloads ?? PreventContextDependentOverloads,
            RequireExplicitTruncationHandling = @override.RequireExplicitTruncationHandling ?? RequireExplicitTruncationHandling,
            EnforceNamespaceDirectoryMapping = @override.EnforceNamespaceDirectoryMapping ?? EnforceNamespaceDirectoryMapping,
            DetectAndBanPhantomDependencies = @override.DetectAndBanPhantomDependencies ?? DetectAndBanPhantomDependencies,
            ImmutabilityExemptSuffixes = @override.ImmutabilityExemptSuffixes ?? ImmutabilityExemptSuffixes,
            ImmutabilityExemptPatterns = @override.ImmutabilityExemptPatterns ?? ImmutabilityExemptPatterns,
            AllowedEmptyReads = @override.AllowedEmptyReads ?? AllowedEmptyReads,
            SealedClassExemptSuffixes = @override.SealedClassExemptSuffixes ?? SealedClassExemptSuffixes,
            ImmutabilityExemptBaseTypes = @override.ImmutabilityExemptBaseTypes ?? ImmutabilityExemptBaseTypes,
            ImmutabilityAllowPrivateBackingFields = @override.ImmutabilityAllowPrivateBackingFields ?? ImmutabilityAllowPrivateBackingFields,
        };
    }
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
    public int MaxMethodOverloads { get; init; } = 3;
    public int MaxConstructorDependencies { get; init; } = 5;
    public int MaxDirectoryDepth { get; init; } = 4;

    /// <summary>
    /// Namespace-Präfixe von Framework-Basistypen, die beim Zählen der Vererbungstiefe ignoriert werden.
    /// Beispiel: ["System.", "System.Windows.", "Microsoft.AspNetCore.Components."]
    /// Leer = alle Typen zählen (bisheriges Verhalten).
    /// </summary>
    public IReadOnlyCollection<string> InheritanceDepthFrameworkPrefixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Typ-Name-Präfixe von Framework-/Cross-Cutting-Abhängigkeiten, die nicht
    /// zu MaxConstructorDependencies zählen.
    /// </summary>
    public IReadOnlyCollection<string> ConstructorDependencyIgnoreTypePrefixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten.
    /// </summary>
    public int MaxAIContextFootprint { get; init; } = 5000;

    /// <summary>
    /// Toleranzbereich über dem Komplexitätslimit für Warning statt Error.
    /// </summary>
    public int ComplexityNearMissTolerance { get; init; } = 0;

    /// <summary>
    /// Switch-Dispatcher-Methoden aus der Komplexitätsmessung ausnehmen.
    /// </summary>
    public bool ExcludeSwitchDispatcherCases { get; init; } = false;

    /// <summary>
    /// Max. Code-Zeilen pro Case/If-Zweig damit er als Dispatcher-Zweig gilt.
    /// </summary>
    public int SwitchDispatcherMaxCaseBodyLines { get; init; } = 3;

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public MetricsConfig Apply(MetricsConfigOverride? @override)
    {
        return this with
        {
            MaxLineCount = @override?.MaxLineCount ?? MaxLineCount,
            MaxMethodParameterCount = @override?.MaxMethodParameterCount ?? MaxMethodParameterCount,
            MaxMethodLineCount = @override?.MaxMethodLineCount ?? MaxMethodLineCount,
            MaxCyclomaticComplexity = @override?.MaxCyclomaticComplexity ?? MaxCyclomaticComplexity,
            MaxCognitiveComplexity = @override?.MaxCognitiveComplexity ?? MaxCognitiveComplexity,
            MaxInheritanceDepth = @override?.MaxInheritanceDepth ?? MaxInheritanceDepth,
            MinCognitiveComplexityForTest = @override?.MinCognitiveComplexityForTest ?? MinCognitiveComplexityForTest,
            AggregatePartialClassLineCount = @override?.AggregatePartialClassLineCount ?? AggregatePartialClassLineCount,
            MaxMethodOverloads = @override?.MaxMethodOverloads ?? MaxMethodOverloads,
            MaxConstructorDependencies = @override?.MaxConstructorDependencies ?? MaxConstructorDependencies,
            MaxAIContextFootprint = @override?.MaxAIContextFootprint ?? MaxAIContextFootprint,
            MaxDirectoryDepth = @override?.MaxDirectoryDepth ?? MaxDirectoryDepth,
            InheritanceDepthFrameworkPrefixes = @override?.InheritanceDepthFrameworkPrefixes ?? InheritanceDepthFrameworkPrefixes,
            ConstructorDependencyIgnoreTypePrefixes = @override?.ConstructorDependencyIgnoreTypePrefixes ?? ConstructorDependencyIgnoreTypePrefixes,
            ComplexityNearMissTolerance = @override?.ComplexityNearMissTolerance ?? ComplexityNearMissTolerance,
            ExcludeSwitchDispatcherCases = @override?.ExcludeSwitchDispatcherCases ?? ExcludeSwitchDispatcherCases,
            SwitchDispatcherMaxCaseBodyLines = @override?.SwitchDispatcherMaxCaseBodyLines ?? SwitchDispatcherMaxCaseBodyLines,
        };
    }
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

    /// <summary>
    /// Überschreibungen der Magic-Value-Erkennung.
    /// </summary>
    public MagicValuesConfigOverride? MagicValues { get; init; }
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

    public bool? EnforceExplicitStateImmutability { get; init; }
    public IReadOnlyCollection<string>? AllowedExceptions { get; init; }
    public bool? EnforceStrictBoundaryForBusinessLogic { get; init; }
    public bool? PreventContextDependentOverloads { get; init; }
    public bool? RequireExplicitTruncationHandling { get; init; }
    public bool? EnforceNamespaceDirectoryMapping { get; init; }
    public bool? DetectAndBanPhantomDependencies { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptPatterns { get; init; }
    public bool? AllowedEmptyReads { get; init; }
    public IReadOnlyCollection<string>? SealedClassExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptBaseTypes { get; init; }
    public bool? ImmutabilityAllowPrivateBackingFields { get; init; }
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

    public int? ComplexityNearMissTolerance { get; init; }
    public bool? ExcludeSwitchDispatcherCases { get; init; }
    public int? SwitchDispatcherMaxCaseBodyLines { get; init; }
}

/// <summary>
/// Fein-granulare Konfiguration der Magic-Value-Erkennung.
/// </summary>
public sealed record MagicValuesConfig
{
    /// <summary>
    /// Steuert welche Literale als magic gelten.
    /// "all"              — alle String+Numeric (bisheriges Verhalten)
    /// "numeric-only"     — nur Numeric-Literale (außer 0/1/-1)
    /// "numeric-and-short-string" — Numeric + Strings bis MinStringLength Zeichen
    /// </summary>
    public string Mode { get; init; } = "all";

    /// <summary>
    /// Mindestlänge eines Strings damit er als magic gilt (bei Mode numeric-and-short-string).
    /// Default 0 = alle Strings (heutiges Verhalten).
    /// </summary>
    public int MinStringLength { get; init; } = 0;

    /// <summary>
    /// Regex-Muster für String-Literale, die grundsätzlich ignoriert werden.
    /// Beispiel: ["^/[\\w/{}\\-]*$"] ignoriert Routen wie "/api/{id}"
    /// </summary>
    public IReadOnlyCollection<string> IgnoreStringPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Erweiterter Satz ignorierter Numeric-Werte (zusätzlich zu 0/1/-1).
    /// Beispiel: [2, 100, 1000] für bekannte Timeout/Batch-Größen.
    /// </summary>
    public IReadOnlyCollection<double> IgnoreNumericValues { get; init; } = Array.Empty<double>();

    /// <summary>
    /// String-Literale als direkte Argumente von Methoden deren Name mit einem der
    /// Einträge in IgnoreInvocationPrefixes beginnt, werden ignoriert.
    /// </summary>
    public IReadOnlyCollection<string> IgnoreInvocationPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Wenn true: Literale innerhalb von Collection/Dictionary-Initialisierern werden ignoriert.
    /// Für Metadata-over-Code-Muster (JSON-Keys, OAuth-Felder).
    /// </summary>
    public bool IgnoreCollectionInitializers { get; init; } = false;

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public MagicValuesConfig Apply(MagicValuesConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            Mode = @override.Mode ?? Mode,
            MinStringLength = @override.MinStringLength ?? MinStringLength,
            IgnoreStringPatterns = @override.IgnoreStringPatterns ?? IgnoreStringPatterns,
            IgnoreNumericValues = @override.IgnoreNumericValues ?? IgnoreNumericValues,
            IgnoreInvocationPrefixes = @override.IgnoreInvocationPrefixes ?? IgnoreInvocationPrefixes,
            IgnoreCollectionInitializers = @override.IgnoreCollectionInitializers ?? IgnoreCollectionInitializers,
        };
    }
}

/// <summary>
/// Optionale Überschreibungen für die Magic-Value-Konfiguration.
/// </summary>
public sealed record MagicValuesConfigOverride
{
    public string? Mode { get; init; }
    public int? MinStringLength { get; init; }
    public IReadOnlyCollection<string>? IgnoreStringPatterns { get; init; }
    public IReadOnlyCollection<double>? IgnoreNumericValues { get; init; }
    public IReadOnlyCollection<string>? IgnoreInvocationPrefixes { get; init; }
    public bool? IgnoreCollectionInitializers { get; init; }
}
