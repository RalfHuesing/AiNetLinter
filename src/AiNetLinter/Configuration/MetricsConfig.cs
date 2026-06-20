#nullable enable
namespace AiNetLinter.Configuration;

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
    /// Methoden, deren Body ausschließlich ein 'return this with { … }' oder
    /// 'return new T { … }' mit Null-Coalescing-Zuweisungen ist, werden von
    /// MaxCyclomaticComplexity und MaxCognitiveComplexity ausgenommen.
    /// Standard: true — diese Methoden sind semantisch flach trotz hohem McCabe-Wert.
    /// </summary>
    public bool ExcludeNullCoalescingInitializerComplexity { get; init; } = true;

    /// <summary>
    /// Maximaler Anteil an nicht-null-coalescing-Ästen damit eine Methode
    /// als NullCoalescingInitializer gilt (0.0–1.0).
    /// Standard: 0.0 — alle Branches müssen ?? oder ?: sein.
    /// </summary>
    public double NullCoalescingInitializerMaxNonCoalescingRatio { get; init; } = 0.0;

    /// <summary>
    /// Maximale Anzahl Arms in einem Switch-Expression oder Labels in einem Switch-Statement.
    /// 0 = deaktiviert. Empfehlung: 10.
    /// Dispatcher-Methoden können mit <see cref="MaxSwitchArmsExcludeDispatcher"/> ausgenommen werden.
    /// </summary>
    public int MaxSwitchArms { get; init; } = 10;

    /// <summary>
    /// Wenn true: Methoden, die als Switch-Dispatcher klassifiziert werden
    /// (<see cref="SwitchDispatcherMaxCaseBodyLines"/>), werden von MaxSwitchArms ausgenommen.
    /// Standard: true.
    /// </summary>
    public bool MaxSwitchArmsExcludeDispatcher { get; init; } = true;

    /// <summary>
    /// Einfache Typnamen (kein Namespace), deren Methoden von MaxSwitchArms ausgenommen werden.
    /// Nützlich für State-Machine-Klassen mit vielen legitimen States.
    /// Standard: [] (keine Ausnahmen).
    /// </summary>
    public IReadOnlyCollection<string> MaxSwitchArmsExemptTypes { get; init; }
        = Array.Empty<string>();

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
    /// Maximale Anzahl verketteter LINQ-Methoden in einer einzelnen Ausdruckskette.
    /// 0 = deaktiviert. Empfehlung: 5 (ab 6 Methoden: warning).
    /// Nur Methoden aus <see cref="LinqMethodNames"/> zählen.
    /// </summary>
    public int MaxLinqChainLength { get; init; } = 0;

    /// <summary>
    /// LINQ-Methoden-Namen, die als Teil einer LINQ-Kette gewertet werden.
    /// Nicht-LINQ-Chains (z. B. Builder-Chains) werden damit von der Prüfung ausgeschlossen.
    /// Konfigurierbar für projektspezifische LINQ-ähnliche APIs (z. B. EF Core Fluent API).
    /// </summary>
    public IReadOnlyCollection<string> LinqMethodNames { get; init; } =
    [
        "Where", "Select", "SelectMany",
        "GroupBy", "GroupJoin", "Join",
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Take", "TakeWhile", "Skip", "SkipWhile",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault",
        "Count", "LongCount", "Any", "All",
        "Distinct", "DistinctBy", "Union", "UnionBy",
        "Intersect", "IntersectBy", "Except", "ExceptBy",
        "Aggregate", "Sum", "Min", "Max", "Average", "MinBy", "MaxBy",
        "ToList", "ToArray", "ToDictionary", "ToHashSet", "ToLookup",
        "Cast", "OfType", "Append", "Prepend", "Reverse",
        "Zip", "Chunk", "Flatten"
    ];

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
        MaxLinqChainLength = o.MaxLinqChainLength ?? MaxLinqChainLength,
        LinqMethodNames = o.LinqMethodNames ?? LinqMethodNames,
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
        ExcludeNullCoalescingInitializerComplexity = o.ExcludeNullCoalescingInitializerComplexity ?? ExcludeNullCoalescingInitializerComplexity,
        NullCoalescingInitializerMaxNonCoalescingRatio = o.NullCoalescingInitializerMaxNonCoalescingRatio ?? NullCoalescingInitializerMaxNonCoalescingRatio,
        MaxSwitchArms = o.MaxSwitchArms ?? MaxSwitchArms,
        MaxSwitchArmsExcludeDispatcher = o.MaxSwitchArmsExcludeDispatcher ?? MaxSwitchArmsExcludeDispatcher,
        MaxSwitchArmsExemptTypes = o.MaxSwitchArmsExemptTypes ?? MaxSwitchArmsExemptTypes,
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
