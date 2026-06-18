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
    public UiSeparationConfig UiSeparation { get; init; } = new();
    public FileFiltersConfig FileFilters { get; init; } = new();
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

    /// <summary>
    /// Wenn true: <c>out</c>-Parameter in privaten Methoden werden nicht gemeldet.
    /// Nützlich wenn private Hilfsmethoden intern das <c>out</c>-Idiom nutzen,
    /// die öffentliche API aber trotzdem sauber gehalten werden soll.
    /// Standard: true (private Methoden sind Implementierungsdetails).
    /// </summary>
    public bool AllowOutParametersInPrivateMethods { get; init; } = true;

    /// <summary>
    /// Methoden-Namen, für die <c>EnforceSemanticNaming</c> nicht geprüft wird.
    /// Nützlich für Standard-C#-Overrides wie <c>Equals(object? obj)</c> oder
    /// <c>CompareTo(object? obj)</c>, bei denen der Parametername konventionell ist.
    /// Standard: ["Equals", "CompareTo", "GetHashCode"] (BCL-Overrides).
    /// </summary>
    public IReadOnlyCollection<string> SemanticNamingExemptMethodNames { get; init; }
        = ["Equals", "CompareTo", "GetHashCode"];

    /// <summary>
    /// Wenn true: Ein Parameter-Name wird von <c>EnforceSemanticNaming</c> nicht gemeldet,
    /// wenn er als Teilstring (case-insensitiv) im Methoden-Namen vorkommt.
    /// Beispiel: Parameter "item" in Methode "AppendTimelineItemAsync" → nicht flaggen.
    /// Standard: true (semantisch korrekt wenn der Kontext im Methodennamen steckt).
    /// </summary>
    public bool SemanticNamingAllowSubstringOfMethodName { get; init; } = true;

    /// <summary>
    /// Exception-Typen, die lautlos abgefangen werden dürfen (leerer catch-Block ohne Variable).
    /// Analogon zu AllowCancellationShutdownCatch für projektspezifische Exception-Typen.
    /// Nur der einfache Typname, kein Namespace (z.B. "JSDisconnectedException").
    /// </summary>
    public IReadOnlyList<string> AllowedSilentCatchExceptionTypes { get; init; } = [];
    public bool EnforceMinimalApiAsParameters { get; init; } = false;
    public bool EnforceResultPatternOverExceptions { get; init; } = true;

    /// <summary>
    /// Namespace-Suffixe für die throw erlaubt ist (z. B. "Infrastructure", "Middleware").
    /// Segment-basierter Match: "MyApp.Infrastructure" endet mit ".Infrastructure".
    /// </summary>
    public IReadOnlyCollection<string> ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Bare "throw;" (Rethrow in Catch-Block) ist immer erlaubt wenn true.
    /// </summary>
    public bool ResultPatternAllowCatchRethrow { get; init; } = true;
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
    public bool PreventContextDependentOverloads { get; init; } = true;
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;

    /// <summary>
    /// Der Modus fuer den Namespace-Ordner-Abgleich: "exact" | "suffix-match" | "contains-all"
    /// </summary>
    public string NamespaceDirectoryMappingMode { get; init; } = "exact";

    /// <summary>
    /// Pfad-Segmente, die beim Namespace-Vergleich ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string> NamespaceDirectoryMappingIgnorePathSegments { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Im Suffix-Match-Modus: Anzahl der letzten Ordner-Segmente, die im Namespace enthalten sein muessen.
    /// </summary>
    public int NamespaceDirectoryMappingRequiredTrailingSegments { get; init; } = 2;

    public bool DetectAndBanPhantomDependencies { get; init; } = true;

    /// <summary>
    /// Verbietet oeffentliche (public/internal) nested Typen innerhalb von Klassen, Records und Structs.
    /// Verbessert die Grep-/File-Listing-Navigation fuer KI-Agenten und verhindert FQN-Halluzinationen
    /// (z. B. <c>PaymentStatus</c> statt <c>PaymentProcessor.PaymentStatus</c>).
    /// Standard: <c>true</c>. Private nested Typen bleiben erlaubt (Implementierungsdetail).
    /// </summary>
    public bool BanPublicNestedTypes { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c> (Standard): <c>private</c> nested Typen bleiben erlaubt, weil sie kein
    /// externes Grep-Target fuer Agenten darstellen. Auf <c>false</c> setzen, um auch private
    /// nested Typen zu melden (strikter Greenfield-Modus).
    /// </summary>
    public bool BanPublicNestedTypesAllowPrivate { get; init; } = true;

    public IReadOnlyCollection<string> ImmutabilityExemptSuffixes { get; init; } = new[]
    {
        "Dto", "Entity", "Model", "Request", "Response", "Command"
    };
    public IReadOnlyCollection<string> ImmutabilityExemptPatterns { get; init; } = Array.Empty<string>();
    public bool AllowedEmptyReads { get; init; } = false;
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; } = Array.Empty<string>();
    public bool ImmutabilityAllowPrivateBackingFields { get; init; } = false;
    public bool EnablePerformanceProfiling { get; init; } = true;

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

    private GlobalConfig ApplyCore1(GlobalConfigOverride @override) =>
        ApplyCore1a(@override).ApplyCore1b(@override);

    private GlobalConfig ApplyCore1a(GlobalConfigOverride @override) => this with
    {
        EnforceSealedClasses = @override.EnforceSealedClasses ?? EnforceSealedClasses,
        AllowUnsealedPartialClasses = @override.AllowUnsealedPartialClasses ?? AllowUnsealedPartialClasses,
        AllowDynamic = @override.AllowDynamic ?? AllowDynamic,
        AllowOutParameters = @override.AllowOutParameters ?? AllowOutParameters,
        EnforceValueObjectContracts = @override.EnforceValueObjectContracts ?? EnforceValueObjectContracts,
        EnableTestSentinel = @override.EnableTestSentinel ?? EnableTestSentinel,
        EnforcePascalCase = @override.EnforcePascalCase ?? EnforcePascalCase,
    };

    private GlobalConfig ApplyCore1b(GlobalConfigOverride @override) => this with
    {
        EnforceXmlDocumentation = @override.EnforceXmlDocumentation ?? EnforceXmlDocumentation,
        EnforceSemanticNaming = @override.EnforceSemanticNaming ?? EnforceSemanticNaming,
        EnforceNullableEnable = @override.EnforceNullableEnable ?? EnforceNullableEnable,
        EnforceNoSilentCatch = @override.EnforceNoSilentCatch ?? EnforceNoSilentCatch,
        AllowTryPatternOutParameters = @override.AllowTryPatternOutParameters ?? AllowTryPatternOutParameters,
        AllowCancellationShutdownCatch = @override.AllowCancellationShutdownCatch ?? AllowCancellationShutdownCatch,
        AllowedSilentCatchExceptionTypes = @override.AllowedSilentCatchExceptionTypes ?? AllowedSilentCatchExceptionTypes,
        EnforceMinimalApiAsParameters = @override.EnforceMinimalApiAsParameters ?? EnforceMinimalApiAsParameters,
        EnforceResultPatternOverExceptions = @override.EnforceResultPatternOverExceptions ?? EnforceResultPatternOverExceptions,
        AllowOutParametersInPrivateMethods = @override.AllowOutParametersInPrivateMethods ?? AllowOutParametersInPrivateMethods,
        SemanticNamingExemptMethodNames = @override.SemanticNamingExemptMethodNames ?? SemanticNamingExemptMethodNames,
    };

    private GlobalConfig ApplyCore2(GlobalConfigOverride @override) =>
        ApplyCore2a(@override).ApplyCore2b(@override).ApplyCore2c(@override);

    private GlobalConfig ApplyCore2a(GlobalConfigOverride @override) => this with
    {
        EnforceNoVariableShadowing = @override.EnforceNoVariableShadowing ?? EnforceNoVariableShadowing,
        EnforceReadonlyParameters = @override.EnforceReadonlyParameters ?? EnforceReadonlyParameters,
        EnforceReadonlyFields = @override.EnforceReadonlyFields ?? EnforceReadonlyFields,
        EnforceNoMagicValues = @override.EnforceNoMagicValues ?? EnforceNoMagicValues,
        EnforceExplicitStateImmutability = @override.EnforceExplicitStateImmutability ?? EnforceExplicitStateImmutability,
        AllowedExceptions = @override.AllowedExceptions ?? AllowedExceptions,
        PreventContextDependentOverloads = @override.PreventContextDependentOverloads ?? PreventContextDependentOverloads,
    };

    private GlobalConfig ApplyCore2b(GlobalConfigOverride @override) => this with
    {
        EnforceNamespaceDirectoryMapping = @override.EnforceNamespaceDirectoryMapping ?? EnforceNamespaceDirectoryMapping,
        NamespaceDirectoryMappingMode = @override.NamespaceDirectoryMappingMode ?? NamespaceDirectoryMappingMode,
        NamespaceDirectoryMappingIgnorePathSegments = @override.NamespaceDirectoryMappingIgnorePathSegments ?? NamespaceDirectoryMappingIgnorePathSegments,
        NamespaceDirectoryMappingRequiredTrailingSegments = @override.NamespaceDirectoryMappingRequiredTrailingSegments ?? NamespaceDirectoryMappingRequiredTrailingSegments,
        DetectAndBanPhantomDependencies = @override.DetectAndBanPhantomDependencies ?? DetectAndBanPhantomDependencies,
        ImmutabilityExemptSuffixes = @override.ImmutabilityExemptSuffixes ?? ImmutabilityExemptSuffixes,
        ImmutabilityExemptPatterns = @override.ImmutabilityExemptPatterns ?? ImmutabilityExemptPatterns,
    };

    private GlobalConfig ApplyCore2c(GlobalConfigOverride @override) => this with
    {
        AllowedEmptyReads = @override.AllowedEmptyReads ?? AllowedEmptyReads,
        SealedClassExemptSuffixes = @override.SealedClassExemptSuffixes ?? SealedClassExemptSuffixes,
        ImmutabilityExemptBaseTypes = @override.ImmutabilityExemptBaseTypes ?? ImmutabilityExemptBaseTypes,
        ImmutabilityAllowPrivateBackingFields = @override.ImmutabilityAllowPrivateBackingFields ?? ImmutabilityAllowPrivateBackingFields,
        ResultPatternAllowThrowInNamespaceSuffixes = @override.ResultPatternAllowThrowInNamespaceSuffixes ?? ResultPatternAllowThrowInNamespaceSuffixes,
        ResultPatternAllowCatchRethrow = @override.ResultPatternAllowCatchRethrow ?? ResultPatternAllowCatchRethrow,
        EnablePerformanceProfiling = @override.EnablePerformanceProfiling ?? EnablePerformanceProfiling,
        SemanticNamingAllowSubstringOfMethodName = @override.SemanticNamingAllowSubstringOfMethodName ?? SemanticNamingAllowSubstringOfMethodName,
        BanPublicNestedTypes = @override.BanPublicNestedTypes ?? BanPublicNestedTypes,
        BanPublicNestedTypesAllowPrivate = @override.BanPublicNestedTypesAllowPrivate ?? BanPublicNestedTypesAllowPrivate,
    };
}

/// <summary>
/// Grenzwerte für verschiedene Code-Metriken.
/// </summary>
public sealed record MetricsConfig
{
    public int MaxLineCount { get; init; } = 500;
    public int MaxMethodParameterCount { get; init; } = 4;

    /// <summary>
    /// Maximale Parameteranzahl pro Methode in Testdateien.
    /// 0 = gleicher Grenzwert wie <see cref="MaxMethodParameterCount"/>.
    /// Empfehlung: 6–8, da Test-Hilfsmethoden (Arrange-Helfer, Browser-Asserts) naturgemäß breiter sind.
    /// </summary>
    public int MaxMethodParameterCountInTestFiles { get; init; } = 0;

    /// <summary>
    /// Typ-Namen (einfacher Name, kein Namespace), die bei <see cref="MaxMethodParameterCount"/> nicht mitgezählt werden.
    /// Standard: [] (alle Parameter zählen). Empfehlung für .NET-Projekte: ["CancellationToken"].
    /// </summary>
    public IReadOnlyCollection<string> MethodParameterCountIgnoreTypeNames { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden.
    /// Ermöglicht z. B. "ILogger" um ILogger<T> auszuschließen.
    /// Standard: [].
    /// </summary>
    public IReadOnlyCollection<string> MethodParameterCountIgnoreTypePrefixes { get; init; }
        = Array.Empty<string>();

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
    /// Klassen-Name-Suffixe, für die MaxConstructorDependencies nicht geprüft wird.
    /// Typisch: ["Exception"] — Exception-Typen haben Payload-Parameter, keine DI-Abhängigkeiten.
    /// </summary>
    public IReadOnlyCollection<string> ConstructorDependencyExemptClassSuffixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten.
    /// </summary>
    public int MaxAIContextFootprint { get; init; } = 5000;

    /// <summary>
    /// Namespace-Präfixe von Typen, die beim Footprint nicht mitgezählt werden.
    /// Nützlich wenn Drittanbieter-Quellcode direkt in der Solution liegt.
    /// Framework-Typen ohne Quellcode (MudBlazor NuGet, System.*) werden immer automatisch ausgeschlossen.
    /// Standard: [].
    /// </summary>
    public IReadOnlyCollection<string> FootprintIgnoreNamespacePrefixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Einfache Typ-Namen (kein Namespace), die bei <see cref="MaxAIContextFootprint"/> nicht mitgezählt werden.
    /// Nützlich für Infrastruktur-Omnipräsenz-Typen, die praktisch überall transitiv vorhanden
    /// sind und das Footprint-Budget strukturell immer ausschöpfen.
    /// Nur einfacher Name (kein Namespace), z. B. "SqlExecutor" nicht "MyApp.Infra.SqlExecutor".
    /// Standard: [] (alle Typen zählen).
    /// </summary>
    public IReadOnlyCollection<string> FootprintIgnoreTypeNames { get; init; }
        = Array.Empty<string>();

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
    /// Maximale Anzahl bool-Parameter in einer öffentlichen Methode/Konstruktor.
    /// 0 = deaktiviert. Private Methoden werden mit <see cref="MaxBoolParameterCountAllowPrivate"/> gesteuert.
    /// </summary>
    public int MaxBoolParameterCount { get; init; } = 0;

    /// <summary>
    /// Wenn true werden private/protected Methoden vom Bool-Parameter-Check ausgenommen.
    /// </summary>
    public bool MaxBoolParameterCountAllowPrivate { get; init; } = true;

    /// <summary>
    /// Methoden-Name-Präfixe, für die MaxBoolParameterCount nicht geprüft wird (z. B. "TryParse").
    /// </summary>
    public IReadOnlyCollection<string> MaxBoolParameterCountExemptMethodPrefixes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Maximale Anzahl direkter Kind-Einträge (Dateien + Unterordner) in einem Verzeichnis.
    /// 0 = deaktiviert. Exemptions: <see cref="MaxDirectoryChildrenExemptNames"/>.
    /// </summary>
    public int MaxDirectoryChildren { get; init; } = 0;

    /// <summary>
    /// Ordnernamen (nicht Pfade), die vom MaxDirectoryChildren-Check ausgenommen werden.
    /// Standard: übliche generierte Ordner wie "Migrations", "Generated", "wwwroot".
    /// </summary>
    public IReadOnlyCollection<string> MaxDirectoryChildrenExemptNames { get; init; }
        = ["Migrations", "Generated", "wwwroot", "obj", "bin", ".git"];

    /// <summary>
    /// Maximale Anzahl Dateien, in denen ein partial-Typ deklariert sein darf.
    /// 0 = deaktiviert. 2 erlaubt das gängige *.g.cs / *.designer.cs Pattern.
    /// </summary>
    public int MaxPartialClassFiles { get; init; } = 0;

    /// <summary>
    /// Typ-Namen (vollqualifiziert oder einfacher Name), die vom MaxPartialClassFiles-Check ausgenommen werden.
    /// </summary>
    public IReadOnlyCollection<string> MaxPartialClassFilesExemptTypes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Maximale Anzahl öffentlicher Member (Methoden + Properties + Events + Felder) pro Typ.
    /// 0 = deaktiviert. Override/Interface-Implementierungen sowie Konstruktoren werden nicht gezählt.
    /// </summary>
    public int MaxPublicMembersPerType { get; init; } = 0;

    /// <summary>
    /// Klassen-Name-Suffixe, für die MaxPublicMembersPerType nicht geprüft wird.
    /// Standard: Extensions/Mapper/Constants sind by Design breit.
    /// </summary>
    public IReadOnlyCollection<string> MaxPublicMembersPerTypeExemptSuffixes { get; init; }
        = ["Extensions", "Mapper", "Constants"];

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public MetricsConfig Apply(MetricsConfigOverride? @override) =>
        ApplyPart1(@override).ApplyPart2a(@override).ApplyPart2b(@override).ApplyPart3(@override);

    private MetricsConfig ApplyPart1(MetricsConfigOverride? @override) => this with
    {
        MaxLineCount = @override?.MaxLineCount ?? MaxLineCount,
        MaxMethodParameterCount = @override?.MaxMethodParameterCount ?? MaxMethodParameterCount,
        MaxMethodParameterCountInTestFiles = @override?.MaxMethodParameterCountInTestFiles ?? MaxMethodParameterCountInTestFiles,
        MethodParameterCountIgnoreTypeNames = @override?.MethodParameterCountIgnoreTypeNames ?? MethodParameterCountIgnoreTypeNames,
        MaxMethodLineCount = @override?.MaxMethodLineCount ?? MaxMethodLineCount,
        MaxCyclomaticComplexity = @override?.MaxCyclomaticComplexity ?? MaxCyclomaticComplexity,
        MaxCognitiveComplexity = @override?.MaxCognitiveComplexity ?? MaxCognitiveComplexity,
        MaxInheritanceDepth = @override?.MaxInheritanceDepth ?? MaxInheritanceDepth,
        MinCognitiveComplexityForTest = @override?.MinCognitiveComplexityForTest ?? MinCognitiveComplexityForTest,
        AggregatePartialClassLineCount = @override?.AggregatePartialClassLineCount ?? AggregatePartialClassLineCount,
        MaxMethodOverloads = @override?.MaxMethodOverloads ?? MaxMethodOverloads,
    };

    private MetricsConfig ApplyPart2a(MetricsConfigOverride? @override) => this with
    {
        MaxConstructorDependencies = @override?.MaxConstructorDependencies ?? MaxConstructorDependencies,
        MaxAIContextFootprint = @override?.MaxAIContextFootprint ?? MaxAIContextFootprint,
        FootprintIgnoreNamespacePrefixes = @override?.FootprintIgnoreNamespacePrefixes ?? FootprintIgnoreNamespacePrefixes,
        FootprintIgnoreTypeNames = @override?.FootprintIgnoreTypeNames ?? FootprintIgnoreTypeNames,
        MethodParameterCountIgnoreTypePrefixes = @override?.MethodParameterCountIgnoreTypePrefixes ?? MethodParameterCountIgnoreTypePrefixes,
        MaxDirectoryDepth = @override?.MaxDirectoryDepth ?? MaxDirectoryDepth,
        InheritanceDepthFrameworkPrefixes = @override?.InheritanceDepthFrameworkPrefixes ?? InheritanceDepthFrameworkPrefixes,
    };

    private MetricsConfig ApplyPart2b(MetricsConfigOverride? @override) => this with
    {
        ConstructorDependencyIgnoreTypePrefixes = @override?.ConstructorDependencyIgnoreTypePrefixes ?? ConstructorDependencyIgnoreTypePrefixes,
        ConstructorDependencyExemptClassSuffixes = @override?.ConstructorDependencyExemptClassSuffixes ?? ConstructorDependencyExemptClassSuffixes,
        ComplexityNearMissTolerance = @override?.ComplexityNearMissTolerance ?? ComplexityNearMissTolerance,
        ExcludeSwitchDispatcherCases = @override?.ExcludeSwitchDispatcherCases ?? ExcludeSwitchDispatcherCases,
        SwitchDispatcherMaxCaseBodyLines = @override?.SwitchDispatcherMaxCaseBodyLines ?? SwitchDispatcherMaxCaseBodyLines,
    };

    private MetricsConfig ApplyPart3(MetricsConfigOverride? @override) => this with
    {
        MaxBoolParameterCount = @override?.MaxBoolParameterCount ?? MaxBoolParameterCount,
        MaxBoolParameterCountAllowPrivate = @override?.MaxBoolParameterCountAllowPrivate ?? MaxBoolParameterCountAllowPrivate,
        MaxBoolParameterCountExemptMethodPrefixes = @override?.MaxBoolParameterCountExemptMethodPrefixes ?? MaxBoolParameterCountExemptMethodPrefixes,
        MaxDirectoryChildren = @override?.MaxDirectoryChildren ?? MaxDirectoryChildren,
        MaxDirectoryChildrenExemptNames = @override?.MaxDirectoryChildrenExemptNames ?? MaxDirectoryChildrenExemptNames,
        MaxPartialClassFiles = @override?.MaxPartialClassFiles ?? MaxPartialClassFiles,
        MaxPartialClassFilesExemptTypes = @override?.MaxPartialClassFilesExemptTypes ?? MaxPartialClassFilesExemptTypes,
        MaxPublicMembersPerType = @override?.MaxPublicMembersPerType ?? MaxPublicMembersPerType,
        MaxPublicMembersPerTypeExemptSuffixes = @override?.MaxPublicMembersPerTypeExemptSuffixes ?? MaxPublicMembersPerTypeExemptSuffixes,
    };
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

    /// <summary>
    /// Klassen deren Name mit einem dieser Suffixe endet, werden vom StaticTestSentinel ausgenommen.
    /// Beispiel: ["Extensions", "Constants", "Converter"]
    /// </summary>
    public IReadOnlyCollection<string> ExemptClassNameSuffixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Klassen die von einem dieser Typen erben oder diese Interfaces implementieren,
    /// werden vom StaticTestSentinel ausgenommen.
    /// Beispiel: ["ComponentBase", "IValueConverter", "Profile"]
    /// </summary>
    public IReadOnlyCollection<string> ExemptWhenInheritsFrom { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Statische Klassen werden vom StaticTestSentinel ausgenommen wenn true.
    /// </summary>
    public bool ExemptStaticClasses { get; init; } = false;

    /// <summary>
    /// Projekt-Name-Suffixe, die ein Projekt als Testprojekt kennzeichnen,
    /// wenn keine bekannten Testrahmenbibliotheken in den Metadaten gefunden wurden.
    /// Standard: ["Tests", "Test", "IntegrationTests", "Specs", "Spec"].
    /// </summary>
    public IReadOnlyList<string> TestProjectNameSuffixes { get; init; }
        = ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// </summary>
    public TestSentinelConfig Apply(TestSentinelConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            ExemptClassNameSuffixes = @override.ExemptClassNameSuffixes ?? ExemptClassNameSuffixes,
            ExemptWhenInheritsFrom = @override.ExemptWhenInheritsFrom ?? ExemptWhenInheritsFrom,
            ExemptStaticClasses = @override.ExemptStaticClasses ?? ExemptStaticClasses,
            TestProjectNameSuffixes = @override.TestProjectNameSuffixes ?? TestProjectNameSuffixes,
        };
    }
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

    /// <summary>
    /// Überschreibungen der TestSentinel-Konfiguration (Ausnahmen vom Testpflicht-Check).
    /// </summary>
    public TestSentinelConfigOverride? TestSentinel { get; init; }

    /// <summary>
    /// Überschreibungen der UI-Trennungsregeln (Blazor/WPF).
    /// </summary>
    public UiSeparationConfigOverride? UiSeparation { get; init; }
}
