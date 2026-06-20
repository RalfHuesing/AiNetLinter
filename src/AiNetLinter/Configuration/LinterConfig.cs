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
    public bool EnforceXmlDocumentation { get; init; } = false;
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
    public IReadOnlyList<string> AllowedSilentCatchExceptionTypes { get; init; } = ["ObjectDisposedException"];
    public bool EnforceMinimalApiAsParameters { get; init; } = false;
    public bool EnforceResultPatternOverExceptions { get; init; } = false;

    /// <summary>
    /// Namespace-Suffixe für die throw erlaubt ist (z. B. "Infrastructure", "Middleware").
    /// Segment-basierter Match: "MyApp.Infrastructure" endet mit ".Infrastructure".
    /// </summary>
    public IReadOnlyCollection<string> ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
        = ["Infrastructure", "Endpoints", "Middleware", "Program"];

    /// <summary>
    /// Bare "throw;" (Rethrow in Catch-Block) ist immer erlaubt wenn true.
    /// </summary>
    public bool ResultPatternAllowCatchRethrow { get; init; } = true;
    public bool EnforceExplicitStateImmutability { get; init; } = false;
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
    public bool PreventContextDependentOverloads { get; init; } = false;
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;

    /// <summary>
    /// Der Modus fuer den Namespace-Ordner-Abgleich: "exact" | "suffix-match" | "contains-all"
    /// </summary>
    public string NamespaceDirectoryMappingMode { get; init; } = "suffix-match";

    /// <summary>
    /// Pfad-Segmente, die beim Namespace-Vergleich ignoriert werden.
    /// </summary>
    public IReadOnlyCollection<string> NamespaceDirectoryMappingIgnorePathSegments { get; init; }
        = ["src", "Source", "Domains", "Handlers"];

    /// <summary>
    /// Im Suffix-Match-Modus: Anzahl der letzten Ordner-Segmente, die im Namespace enthalten sein muessen.
    /// </summary>
    public int NamespaceDirectoryMappingRequiredTrailingSegments { get; init; } = 2;

    public bool DetectAndBanPhantomDependencies { get; init; } = false;

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
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; } = ["Base", "Foundation", "Host"];
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; } =
    [
        "ComponentBase",
        "LayoutComponentBase",
        "ObservableObject",
        "ObservableRecipient",
        "BackgroundService",
        "AuthenticationStateProvider",
        "INotifyPropertyChanged"
    ];
    public bool ImmutabilityAllowPrivateBackingFields { get; init; } = true;
    public bool EnablePerformanceProfiling { get; init; } = true;

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    // ainetlinter-disable MaxCyclomaticComplexity
    // ainetlinter-disable MaxCognitiveComplexity
    public GlobalConfig Apply(GlobalConfigOverride? @override)
    {
        if (@override == null) return this;
        var o = @override;
        return this with
        {
            // Strukturregeln
            EnforceSealedClasses                        = o.EnforceSealedClasses                        ?? EnforceSealedClasses,
            AllowUnsealedPartialClasses                 = o.AllowUnsealedPartialClasses                 ?? AllowUnsealedPartialClasses,
            AllowDynamic                                = o.AllowDynamic                                ?? AllowDynamic,
            AllowOutParameters                          = o.AllowOutParameters                          ?? AllowOutParameters,
            AllowTryPatternOutParameters                = o.AllowTryPatternOutParameters                ?? AllowTryPatternOutParameters,
            AllowOutParametersInPrivateMethods          = o.AllowOutParametersInPrivateMethods          ?? AllowOutParametersInPrivateMethods,
            SealedClassExemptSuffixes                   = o.SealedClassExemptSuffixes                   ?? SealedClassExemptSuffixes,

            // Naming und Stil
            EnforcePascalCase                           = o.EnforcePascalCase                           ?? EnforcePascalCase,
            EnforceSemanticNaming                       = o.EnforceSemanticNaming                       ?? EnforceSemanticNaming,
            SemanticNamingExemptMethodNames             = o.SemanticNamingExemptMethodNames             ?? SemanticNamingExemptMethodNames,
            SemanticNamingAllowSubstringOfMethodName    = o.SemanticNamingAllowSubstringOfMethodName    ?? SemanticNamingAllowSubstringOfMethodName,
            EnforceNullableEnable                       = o.EnforceNullableEnable                       ?? EnforceNullableEnable,
            EnforceXmlDocumentation                     = o.EnforceXmlDocumentation                     ?? EnforceXmlDocumentation,
            EnforceMinimalApiAsParameters               = o.EnforceMinimalApiAsParameters               ?? EnforceMinimalApiAsParameters,
            EnableTestSentinel                          = o.EnableTestSentinel                          ?? EnableTestSentinel,

            // Catch-Regeln
            EnforceNoSilentCatch                        = o.EnforceNoSilentCatch                        ?? EnforceNoSilentCatch,
            AllowCancellationShutdownCatch              = o.AllowCancellationShutdownCatch              ?? AllowCancellationShutdownCatch,
            AllowedSilentCatchExceptionTypes            = o.AllowedSilentCatchExceptionTypes            ?? AllowedSilentCatchExceptionTypes,
            EnforceResultPatternOverExceptions          = o.EnforceResultPatternOverExceptions          ?? EnforceResultPatternOverExceptions,
            ResultPatternAllowThrowInNamespaceSuffixes  = o.ResultPatternAllowThrowInNamespaceSuffixes  ?? ResultPatternAllowThrowInNamespaceSuffixes,
            ResultPatternAllowCatchRethrow              = o.ResultPatternAllowCatchRethrow              ?? ResultPatternAllowCatchRethrow,
            AllowedExceptions                           = o.AllowedExceptions                           ?? AllowedExceptions,

            // Immutabilität
            EnforceValueObjectContracts                 = o.EnforceValueObjectContracts                 ?? EnforceValueObjectContracts,
            EnforceExplicitStateImmutability            = o.EnforceExplicitStateImmutability            ?? EnforceExplicitStateImmutability,
            ImmutabilityExemptSuffixes                  = o.ImmutabilityExemptSuffixes                  ?? ImmutabilityExemptSuffixes,
            ImmutabilityExemptPatterns                  = o.ImmutabilityExemptPatterns                  ?? ImmutabilityExemptPatterns,
            ImmutabilityExemptBaseTypes                 = o.ImmutabilityExemptBaseTypes                 ?? ImmutabilityExemptBaseTypes,
            ImmutabilityAllowPrivateBackingFields       = o.ImmutabilityAllowPrivateBackingFields       ?? ImmutabilityAllowPrivateBackingFields,
            AllowedEmptyReads                           = o.AllowedEmptyReads                           ?? AllowedEmptyReads,

            // Namespace- und Analyse-Regeln
            EnforceNamespaceDirectoryMapping            = o.EnforceNamespaceDirectoryMapping            ?? EnforceNamespaceDirectoryMapping,
            NamespaceDirectoryMappingMode               = o.NamespaceDirectoryMappingMode               ?? NamespaceDirectoryMappingMode,
            NamespaceDirectoryMappingIgnorePathSegments = o.NamespaceDirectoryMappingIgnorePathSegments ?? NamespaceDirectoryMappingIgnorePathSegments,
            NamespaceDirectoryMappingRequiredTrailingSegments = o.NamespaceDirectoryMappingRequiredTrailingSegments ?? NamespaceDirectoryMappingRequiredTrailingSegments,
            DetectAndBanPhantomDependencies             = o.DetectAndBanPhantomDependencies             ?? DetectAndBanPhantomDependencies,
            PreventContextDependentOverloads            = o.PreventContextDependentOverloads            ?? PreventContextDependentOverloads,
            BanPublicNestedTypes                        = o.BanPublicNestedTypes                        ?? BanPublicNestedTypes,
            BanPublicNestedTypesAllowPrivate            = o.BanPublicNestedTypesAllowPrivate            ?? BanPublicNestedTypesAllowPrivate,
            EnablePerformanceProfiling                  = o.EnablePerformanceProfiling                  ?? EnablePerformanceProfiling,
        };
    }
}

/// <summary>
/// Grenzwerte für verschiedene Code-Metriken.
/// </summary>
public sealed record MetricsConfig
{
    public int MaxLineCount { get; init; } = 700;
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
        = ["CancellationToken"];

    /// <summary>
    /// Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden.
    /// Ermöglicht z. B. "ILogger" um ILogger<T> auszuschließen.
    /// Standard: [].
    /// </summary>
    public IReadOnlyCollection<string> MethodParameterCountIgnoreTypePrefixes { get; init; }
        = Array.Empty<string>();

    public int MaxMethodLineCount { get; init; } = 60;
    public int MaxCyclomaticComplexity { get; init; } = 12;
    public int MaxCognitiveComplexity { get; init; } = 15;
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
        = [
            "ILogger",
            "IOptions",
            "IOptionsSnapshot",
            "IOptionsMonitor",
            "IHostEnvironment",
            "IWebHostEnvironment",
            "IConfiguration",
            "IServiceProvider",
            "IHttpContextAccessor"
        ];

    /// <summary>
    /// Klassen-Name-Suffixe, für die MaxConstructorDependencies nicht geprüft wird.
    /// Typisch: ["Exception"] — Exception-Typen haben Payload-Parameter, keine DI-Abhängigkeiten.
    /// </summary>
    public IReadOnlyCollection<string> ConstructorDependencyExemptClassSuffixes { get; init; }
        = ["Exception"];

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
    public int ComplexityNearMissTolerance { get; init; } = 1;

    /// <summary>
    /// Switch-Dispatcher-Methoden aus der Komplexitätsmessung ausnehmen.
    /// </summary>
    public bool ExcludeSwitchDispatcherCases { get; init; } = true;

    /// <summary>
    /// Max. Code-Zeilen pro Case/If-Zweig damit er als Dispatcher-Zweig gilt.
    /// </summary>
    public int SwitchDispatcherMaxCaseBodyLines { get; init; } = 3;

    /// <summary>
    /// Maximale Anzahl bool-Parameter in einer öffentlichen Methode/Konstruktor.
    /// 0 = deaktiviert. Private Methoden werden mit <see cref="MaxBoolParameterCountAllowPrivate"/> gesteuert.
    /// </summary>
    public int MaxBoolParameterCount { get; init; } = 1;

    /// <summary>
    /// Wenn true werden private/protected Methoden vom Bool-Parameter-Check ausgenommen.
    /// </summary>
    public bool MaxBoolParameterCountAllowPrivate { get; init; } = true;

    /// <summary>
    /// Methoden-Name-Präfixe, für die MaxBoolParameterCount nicht geprüft wird (z. B. "TryParse").
    /// </summary>
    public IReadOnlyCollection<string> MaxBoolParameterCountExemptMethodPrefixes { get; init; }
        = ["Try"];

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
    public int MaxPartialClassFiles { get; init; } = 2;

    /// <summary>
    /// Typ-Namen (vollqualifiziert oder einfacher Name), die vom MaxPartialClassFiles-Check ausgenommen werden.
    /// </summary>
    public IReadOnlyCollection<string> MaxPartialClassFilesExemptTypes { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Maximale Anzahl öffentlicher Member (Methoden + Properties + Events + Felder) pro Typ.
    /// 0 = deaktiviert. Override/Interface-Implementierungen sowie Konstruktoren werden nicht gezählt.
    /// </summary>
    public int MaxPublicMembersPerType { get; init; } = 15;

    /// <summary>
    /// Klassen-Name-Suffixe, für die MaxPublicMembersPerType nicht geprüft wird.
    /// Standard: Extensions/Mapper/Constants sind by Design breit.
    /// </summary>
    public IReadOnlyCollection<string> MaxPublicMembersPerTypeExemptSuffixes { get; init; }
        = ["Extensions", "Mapper", "Constants", "Config", "ConfigOverride", "Args"];

    /// <summary>
    /// Kontextabhängige Suppression von Regeln wenn koinzidente Metriken niedrig sind.
    /// Reduziert False Positives bei strukturell langen aber semantisch einfachen Methoden/Klassen.
    /// Standard: eine Suppression für MaxMethodLineCount bei CC ≤ 3 und CogC ≤ 5.
    /// </summary>
    public IReadOnlyList<CompoundSuppression> CompoundSuppressions { get; init; } =
    [
        new CompoundSuppression
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf =
            [
                new MetricCondition { Metric = "CyclomaticComplexity", AtMost = 3 },
                new MetricCondition { Metric = "CognitiveComplexity",  AtMost = 5 }
            ],
            RelaxedLimit = 150,
            Reason = "Initialisierungs- und Builder-Methoden sind semantisch flach. LOC bei CC≤3 ist nicht mit Fehleranfälligkeit korreliert (Palomba et al., 2018)."
        }
    ];

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public MetricsConfig Apply(MetricsConfigOverride? @override)
    {
        if (@override == null) return this;
        return ApplyLineLimits(@override)
            .ApplyComplexityLimits(@override)
            .ApplyDependencyLimits(@override)
            .ApplyDirectoryAndMemberLimits(@override);
    }

    private MetricsConfig ApplyLineLimits(MetricsConfigOverride o) => this with
    {
        MaxLineCount = o.MaxLineCount ?? MaxLineCount,
        MaxMethodLineCount = o.MaxMethodLineCount ?? MaxMethodLineCount,
        MaxMethodParameterCount = o.MaxMethodParameterCount ?? MaxMethodParameterCount,
        MaxMethodParameterCountInTestFiles = o.MaxMethodParameterCountInTestFiles ?? MaxMethodParameterCountInTestFiles,
        MethodParameterCountIgnoreTypeNames = o.MethodParameterCountIgnoreTypeNames ?? MethodParameterCountIgnoreTypeNames,
        MethodParameterCountIgnoreTypePrefixes = o.MethodParameterCountIgnoreTypePrefixes ?? MethodParameterCountIgnoreTypePrefixes,
        MaxMethodOverloads = o.MaxMethodOverloads ?? MaxMethodOverloads,
        CompoundSuppressions = o.CompoundSuppressions ?? CompoundSuppressions,
    };

    private MetricsConfig ApplyComplexityLimits(MetricsConfigOverride o) => this with
    {
        MaxCyclomaticComplexity = o.MaxCyclomaticComplexity ?? MaxCyclomaticComplexity,
        MaxCognitiveComplexity = o.MaxCognitiveComplexity ?? MaxCognitiveComplexity,
        MinCognitiveComplexityForTest = o.MinCognitiveComplexityForTest ?? MinCognitiveComplexityForTest,
        AggregatePartialClassLineCount = o.AggregatePartialClassLineCount ?? AggregatePartialClassLineCount,
        ComplexityNearMissTolerance = o.ComplexityNearMissTolerance ?? ComplexityNearMissTolerance,
        ExcludeSwitchDispatcherCases = o.ExcludeSwitchDispatcherCases ?? ExcludeSwitchDispatcherCases,
        SwitchDispatcherMaxCaseBodyLines = o.SwitchDispatcherMaxCaseBodyLines ?? SwitchDispatcherMaxCaseBodyLines,
    };

    private MetricsConfig ApplyDependencyLimits(MetricsConfigOverride o) => this with
    {
        MaxConstructorDependencies = o.MaxConstructorDependencies ?? MaxConstructorDependencies,
        ConstructorDependencyIgnoreTypePrefixes = o.ConstructorDependencyIgnoreTypePrefixes ?? ConstructorDependencyIgnoreTypePrefixes,
        ConstructorDependencyExemptClassSuffixes = o.ConstructorDependencyExemptClassSuffixes ?? ConstructorDependencyExemptClassSuffixes,
        MaxInheritanceDepth = o.MaxInheritanceDepth ?? MaxInheritanceDepth,
        InheritanceDepthFrameworkPrefixes = o.InheritanceDepthFrameworkPrefixes ?? InheritanceDepthFrameworkPrefixes,
        MaxAIContextFootprint = o.MaxAIContextFootprint ?? MaxAIContextFootprint,
        FootprintIgnoreNamespacePrefixes = o.FootprintIgnoreNamespacePrefixes ?? FootprintIgnoreNamespacePrefixes,
        FootprintIgnoreTypeNames = o.FootprintIgnoreTypeNames ?? FootprintIgnoreTypeNames,
    };

    private MetricsConfig ApplyDirectoryAndMemberLimits(MetricsConfigOverride o) => this with
    {
        MaxDirectoryDepth = o.MaxDirectoryDepth ?? MaxDirectoryDepth,
        MaxDirectoryChildren = o.MaxDirectoryChildren ?? MaxDirectoryChildren,
        MaxDirectoryChildrenExemptNames = o.MaxDirectoryChildrenExemptNames ?? MaxDirectoryChildrenExemptNames,
        MaxBoolParameterCount = o.MaxBoolParameterCount ?? MaxBoolParameterCount,
        MaxBoolParameterCountAllowPrivate = o.MaxBoolParameterCountAllowPrivate ?? MaxBoolParameterCountAllowPrivate,
        MaxBoolParameterCountExemptMethodPrefixes = o.MaxBoolParameterCountExemptMethodPrefixes ?? MaxBoolParameterCountExemptMethodPrefixes,
        MaxPartialClassFiles = o.MaxPartialClassFiles ?? MaxPartialClassFiles,
        MaxPartialClassFilesExemptTypes = o.MaxPartialClassFilesExemptTypes ?? MaxPartialClassFilesExemptTypes,
        MaxPublicMembersPerType = o.MaxPublicMembersPerType ?? MaxPublicMembersPerType,
        MaxPublicMembersPerTypeExemptSuffixes = o.MaxPublicMembersPerTypeExemptSuffixes ?? MaxPublicMembersPerTypeExemptSuffixes,
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
    public IReadOnlyCollection<string> ExemptClassNameSuffixes { get; init; }
        = ["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"];

    /// <summary>
    /// Klassen die von einem dieser Typen erben oder diese Interfaces implementieren,
    /// werden vom StaticTestSentinel ausgenommen.
    /// Beispiel: ["ComponentBase", "IValueConverter", "Profile"]
    /// </summary>
    public IReadOnlyCollection<string> ExemptWhenInheritsFrom { get; init; }
        = ["ComponentBase", "IValueConverter", "Profile"];

    /// <summary>
    /// Statische Klassen werden vom StaticTestSentinel ausgenommen wenn true.
    /// </summary>
    public bool ExemptStaticClasses { get; init; } = true;

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
    /// Optionaler Freitext-Grund. Wird in .mdc-Output und Violation-Guidance wiedergegeben.
    /// </summary>
    public string? Reason { get; init; }
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
